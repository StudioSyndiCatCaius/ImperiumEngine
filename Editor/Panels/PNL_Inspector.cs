using System.Collections;
using System.Numerics;
using System.Reflection;
using Editor.Dialog;
using Editor.UI;
using Editor.Windows;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Globals;
using Engine.Interfaces;
using Engine.Structs;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Panels;

public class PNL_Inspector : EdPanel
{
    public EUI_SearchBar property_search = new();
    
    public object? selected_object = null;
    public Type? inspect_type;
    public bool static_config;
    public bool config_only;
    public ImpComp? tree_root;
    public Action<ImpComp?>? on_select;
    public Action? on_changed;

    string _rename_buf = "";
    bool _rename;
    bool _rename_focus;
    object? _rename_target;
    float _comp_tree_h = 72f;

    static readonly uint ColX = ImGui.ColorConvertFloat4ToU32(new Vector4(0.86f, 0.24f, 0.24f, 1f));
    static readonly uint ColY = ImGui.ColorConvertFloat4ToU32(new Vector4(0.24f, 0.72f, 0.28f, 1f));
    static readonly uint ColZ = ImGui.ColorConvertFloat4ToU32(new Vector4(0.26f, 0.46f, 0.90f, 1f));
    static readonly uint ColW = ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.75f, 0.20f, 1f));
    static readonly uint[] AxisCols = { ColX, ColY, ColZ, ColW };

    public override void OnDrawPanel()
    {
        property_search.OnDraw();

        if (selected_object == null && inspect_type == null)
        {
            ImGui.TextDisabled("Nothing selected");
            return;
        }

        bool locked = selected_object is ImpComp lc && lc.Editor_IsLocked();
        if (locked) ImGui.BeginDisabled();

        DrawInspectHeader();
        if (!static_config) DrawCompTree();

        DrawMembers(selected_object);

        if (locked) ImGui.EndDisabled();
    }

    void DrawInspectHeader()
    {
        Type? type = inspect_type ?? selected_object?.GetType();
        if (type == null) return;
        ImpComp? comp = selected_object as ImpComp;
        Texture2D ico = TypeIcon(type);
        float th = ImGui.GetTextLineHeight();
        if (ico.Id != 0)
        {
            ImGui.Image((IntPtr)ico.Id, new Vector2(th, th));
            ImGui.SameLine();
        }

        bool hot = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (comp != null && hot && !ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey.F2))
            BeginRename(comp);
        if (_rename && !ReferenceEquals(_rename_target, selected_object))
            _rename = false;

        if (comp != null && _rename)
        {
            if (_rename_focus)
            {
                ImGui.SetKeyboardFocusHere();
                _rename_focus = false;
            }
            ImGui.SetNextItemWidth(-1);
            bool enter = ImGui.InputText("##comp_ren", ref _rename_buf, 128,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
            if (enter)
                CommitRename(comp);
            else if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                    _rename = false;
                else
                    CommitRename(comp);
            }
        }
        else
        {
            string title = type.GetCustomAttribute<TitleAttribute>()?.Name ?? type.Name;
            if (comp != null && !string.IsNullOrEmpty(comp.name))
                title = comp.name + "  (" + type.Name + ")";
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(title);
        }
    }

    public void BeginRename(ImpComp? comp = null)
    {
        comp ??= selected_object as ImpComp;
        if (comp == null) return;
        selected_object = comp;
        _rename = true;
        _rename_focus = true;
        _rename_target = comp;
        _rename_buf = comp.name ?? "";
    }

    void CommitRename(ImpComp comp)
    {
        _rename = false;
        string n = (_rename_buf ?? "").Trim();
        if (n == comp.name) return;
        comp.name = n;
        MarkDirty();
    }

    void DrawCompTree()
    {
        ImpComp? root = tree_root ?? selected_object as ImpComp;
        if (root == null || !HasNativeKids(root)) return;

        ImGui.SeparatorText("Components");
        float max_h = MathF.Max(48f, ImGui.GetContentRegionAvail().Y * 0.5f);
        _comp_tree_h = Math.Clamp(_comp_tree_h, 48f, max_h);
        ImGui.BeginChild("##comp_tree", new Vector2(0, _comp_tree_h), true);
        DrawNativeNode(root);
        ImGui.EndChild();
        ImGui.InvisibleButton("##comp_tree_split", new Vector2(MathF.Max(1f, ImGui.GetContentRegionAvail().X), 5f));
        if (ImGui.IsItemActive())
            _comp_tree_h = Math.Clamp(_comp_tree_h + ImGui.GetIO().MouseDelta.Y, 48f, max_h);
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
    }

    static bool HasNativeKids(ImpComp c)
    {
        for (int i = 0; i < c.children.Count; i++)
        {
            ImpComp k = c.children[i];
            if (k.is_builtin || k.is_child_of_prefab) return true;
        }
        return false;
    }

    void DrawNativeNode(ImpComp c)
    {
        bool has_kids = false;
        for (int i = 0; i < c.children.Count; i++)
        {
            ImpComp k = c.children[i];
            if (k.is_builtin || k.is_child_of_prefab) { has_kids = true; break; }
        }

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.DefaultOpen |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.SpanAvailWidth |
            ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (!has_kids) flags |= ImGuiTreeNodeFlags.Leaf;
        if (c == selected_object) flags |= ImGuiTreeNodeFlags.Selected;

        string label = string.IsNullOrEmpty(c.name) ? c.GetType().Name : c.name;
        ImGui.PushID(c.GetHashCode());
        ImGui.BeginGroup();
        bool open = ImGui.TreeNodeEx("##n", flags);
        ImGui.SameLine(0, 2);
        float icon_sz = ImGui.GetTextLineHeight();
        Texture2D ico = TypeIcon(c.GetType());
        if (ico.Id != 0)
        {
            ImGui.Image((IntPtr)ico.Id, new Vector2(icon_sz, icon_sz));
            ImGui.SameLine(0, 4);
        }
        if (ImGui.Selectable(label, c == selected_object))
        {
            if (on_select != null) on_select(c);
            else selected_object = c;
        }
        ImGui.EndGroup();

        if (open && has_kids)
        {
            ImGui.Indent(18f);
            for (int i = 0; i < c.children.Count; i++)
            {
                ImpComp k = c.children[i];
                if (k.is_builtin || k.is_child_of_prefab)
                    DrawNativeNode(k);
            }
            ImGui.Unindent(18f);
        }
        ImGui.PopID();
    }

    static bool BeginInspectorTable()
    {
        ImGuiTableFlags flags =
            ImGuiTableFlags.BordersInnerV |
            ImGuiTableFlags.Resizable |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("##inspector", 3, flags))
            return false;
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##rv", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, 26f);
        return true;
    }

    static readonly Dictionary<Type, object?> Prototypes = new();
    static readonly Dictionary<Type, Texture2D> TypeIcons = new();

    void DrawMembers(object? target, bool own_tables = true)
    {
        Type? root_type = inspect_type ?? target?.GetType();
        if (root_type == null) return;

        List<(string key, string title, Type? icon, MemberInfo member)> rows = new();
        List<Type> chain = new();
        if (static_config)
            chain.Add(root_type);
        else
            for (Type? t = root_type; t != null && t != typeof(object); t = t.BaseType)
                chain.Add(t);

        bool want_instance = !static_config;
        bool want_static = static_config || config_only;
        bool require_config = static_config || config_only;

        foreach (Type type in chain)
        {
            string class_title = type.GetCustomAttribute<TitleAttribute>()?.Name ?? type.Name;
            void Add(MemberInfo member)
            {
                ImpVarAttribute? attr = member.GetCustomAttribute<ImpVarAttribute>(false);
                if (attr == null || attr.Hidden) return;
                if (require_config && member.GetCustomAttribute<ConfigAttribute>(false) == null) return;
                string? cat = member.GetCustomAttribute<CategoryAttribute>()?.Name;
                bool cls = string.IsNullOrEmpty(cat);
                rows.Add((cls ? (type.FullName ?? type.Name) : cat!, cls ? class_title : cat!, cls ? type : null, member));
            }

            if (want_instance)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    Add(field);
                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    Add(prop);
                }
            }

            if (want_static)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    Add(field);
                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    Add(prop);
                }
            }
        }

        List<(string key, string title, Type? icon)> cats = new();
        Dictionary<string, List<MemberInfo>> map = new();
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (!map.TryGetValue(r.key, out List<MemberInfo>? list))
            {
                list = new List<MemberInfo>();
                map[r.key] = list;
                cats.Add((r.key, r.title, r.icon));
            }
            list.Add(r.member);
        }

        string q = property_search.search_text ?? "";
        bool filtering = !string.IsNullOrWhiteSpace(q);
        object? proto = (static_config || target == null) ? null : Prototype(target.GetType());
        for (int c = 0; c < cats.Count; c++)
        {
            var cat = cats[c];
            List<MemberInfo> members = map[cat.key];
            if (filtering)
            {
                bool cat_hit = cat.title.Contains(q, StringComparison.OrdinalIgnoreCase);
                if (!cat_hit)
                {
                    List<MemberInfo> hit = new();
                    for (int i = 0; i < members.Count; i++)
                        if (MemberHit(members[i], q)) hit.Add(members[i]);
                    if (hit.Count == 0) continue;
                    members = hit;
                }
            }

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.92f, 0.92f, 0.94f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1f, 1f, 1f, 0.12f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1f, 1f, 1f, 0.18f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(1f, 1f, 1f, 0.24f));
            Texture2D ico = cat.icon != null ? TypeIcon(cat.icon) : default;
            if (own_tables)
            {
                if (filtering) ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                bool open = ImGui.TreeNodeEx(" ###" + cat.key,
                    ImGuiTreeNodeFlags.DefaultOpen |
                    ImGuiTreeNodeFlags.SpanAvailWidth |
                    ImGuiTreeNodeFlags.FramePadding |
                    ImGuiTreeNodeFlags.Framed |
                    ImGuiTreeNodeFlags.NoTreePushOnOpen |
                    ImGuiTreeNodeFlags.AllowItemOverlap);
                Vector2 rmin = ImGui.GetItemRectMin();
                Vector2 rmax = ImGui.GetItemRectMax();
                float x = rmin.X + ImGui.GetTreeNodeToLabelSpacing();
                float cy = (rmin.Y + rmax.Y) * 0.5f;
                float th = ImGui.GetTextLineHeight();
                ImDrawListPtr dl = ImGui.GetWindowDrawList();
                if (ico.Id != 0)
                {
                    dl.AddImage((IntPtr)ico.Id, new Vector2(x, cy - th * 0.5f), new Vector2(x + th, cy + th * 0.5f));
                    x += th + 4f;
                }
                dl.AddText(new Vector2(x, cy - th * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), cat.title);
                ImGui.PopStyleColor(4);
                if (!open) continue;
                if (!BeginInspectorTable()) continue;
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
                ImGui.TableSetColumnIndex(0);
                if (ico.Id != 0)
                {
                    float sz = ImGui.GetTextLineHeight();
                    ImGui.Image((IntPtr)ico.Id, new Vector2(sz, sz));
                    ImGui.SameLine(0, 4);
                }
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(cat.title);
                ImGui.PopStyleColor(4);
            }

            ImGui.PushID(cat.key);
            for (int i = 0; i < members.Count; i++)
                DrawInspectMember(target, members[i], proto);
            ImGui.PopID();

            if (own_tables)
                ImGui.EndTable();
        }
    }

    static string MemberLabel(MemberInfo member)
    {
        ImpVarAttribute? attr = member.GetCustomAttribute<ImpVarAttribute>(false);
        return attr?.Name ?? member.GetCustomAttribute<TitleAttribute>()?.Name ?? member.Name;
    }

    static Type MemberType(MemberInfo member) =>
        member is FieldInfo f ? f.FieldType : ((PropertyInfo)member).PropertyType;

    static bool MemberHit(MemberInfo member, string q)
    {
        if (MemberLabel(member).Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        if (member.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        Type t = MemberType(member);
        if (t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        if (!IsExpandableStruct(t)) return false;
        foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
            string? n = field.GetCustomAttribute<ImpVarAttribute>(true)?.Name
                ?? field.GetCustomAttribute<TitleAttribute>()?.Name;
            if (n != null && n.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    void DrawInspectMember(object? target, MemberInfo member, object? proto)
    {
        if (member is FieldInfo field)
        {
            object? host = field.IsStatic ? null : target;
            object? value = field.GetValue(host);
            string label = MemberLabel(field);
            object? def = proto != null && !field.IsStatic ? field.GetValue(proto) : null;
            ImGui.PushID(field.Name);
            if (DrawRow(label, field.FieldType, ref value, v =>
                {
                    field.SetValue(host, v);
                    MarkDirty();
                }, null, proto != null && !field.IsStatic, def))
            {
                field.SetValue(host, value);
                MarkDirty();
            }
            ImGui.PopID();
            return;
        }

        if (member is PropertyInfo prop)
        {
            bool is_static = prop.GetMethod?.IsStatic == true;
            object? host = is_static ? null : target;
            object? value = prop.GetValue(host);
            string label = MemberLabel(prop);
            object? def = proto != null && !is_static ? prop.GetValue(proto) : null;
            ImGui.PushID(prop.Name);
            if (DrawRow(label, prop.PropertyType, ref value, v =>
                {
                    prop.SetValue(host, v);
                    MarkDirty();
                }, null, proto != null && !is_static, def))
            {
                prop.SetValue(host, value);
                MarkDirty();
            }
            ImGui.PopID();
        }
    }

    static object? Prototype(Type t)
    {
        if (Prototypes.TryGetValue(t, out object? p)) return p;
        try { p = Activator.CreateInstance(t); }
        catch { p = null; }
        Prototypes[t] = p;
        return p;
    }

    public static Texture2D TypeIcon(Type t)
    {
        if (TypeIcons.TryGetValue(t, out Texture2D cached)) return cached;
        for (Type? cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
        {
            string abs = GFile.Make_Path_Absolute("{engine}Editor/Types/" + cur.Name + ".png");
            if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) continue;
            try { cached = Raylib.LoadTexture(abs); }
            catch { cached = default; }
            TypeIcons[t] = cached;
            return cached;
        }
        TypeIcons[t] = default;
        return default;
    }

    void MarkDirty()
    {
        if (selected_object is Engine.Core.ImpComp c && c.scene != null)
            c.scene.is_dity = true;
        else if (selected_object is Engine.Core.ImpAsset a)
            a.is_dity = true;
        on_changed?.Invoke();
    }

    static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    bool DrawRow(string label, Type t, ref object? value, Action<object?>? apply = null, Action? tools = null,
        bool can_revert = false, object? revert = null)
    {
        if (value is I_Property ip && ip.Property_Inspector_Override())
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);

            ImGui.TableSetColumnIndex(1);
            ReserveTools(tools);
            object? before = t.IsValueType ? MemberwiseCloneMethod.Invoke(value, null) : null;
            ip.Property_Inspector_Draw();
            value = ip;
            DrawTools(tools);
            bool changed = t.IsValueType && !Equals(before, value);
            if (DrawRevertCol(ref value, can_revert, revert, apply)) changed = true;
            if (changed) apply?.Invoke(value);
            return changed;
        }

        if (IsTClass(t))
            return DrawClassRow(label, t, ref value, apply, tools, can_revert, revert);

        if (IsTRef(t))
            return DrawRefRow(label, t, ref value, apply, tools, can_revert, revert);

        if (typeof(ImpAsset).IsAssignableFrom(t))
            return DrawAssetRow(label, t, ref value, apply, tools, can_revert, revert);

        if (IsList(t))
            return DrawListRow(label, t, ref value, apply, tools, can_revert, revert);

        if (IsDict(t))
            return DrawDictRow(label, t, ref value, apply, tools, can_revert, revert);

        if (IsExpandableStruct(t))
            return DrawStructRow(label, t, ref value, apply, tools, can_revert, revert);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);

        ImGui.TableSetColumnIndex(1);
        ReserveTools(tools);
        bool edited = DrawEditor(t, ref value);
        DrawTools(tools);
        if (DrawRevertCol(ref value, can_revert, revert, apply)) edited = true;
        if (!edited) return false;
        apply?.Invoke(value);
        return true;
    }

    static bool DrawRevertCol(ref object? value, bool can_revert, object? revert, Action<object?>? apply)
    {
        ImGui.TableSetColumnIndex(2);
        if (!can_revert || ValuesEqual(value, revert)) return false;
        if (!ImGui.SmallButton("↺"))
        {
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Revert to class default");
            return false;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Revert to class default");
        value = CloneForAssign(revert);
        apply?.Invoke(value);
        return true;
    }

    static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a is ImpAsset aa && b is ImpAsset bb)
        {
            if (aa.is_inlined || bb.is_inlined) return false;
            if (!string.IsNullOrEmpty(aa.filepath) && aa.filepath == bb.filepath) return true;
            return false;
        }
        if (IsList(a.GetType()) && IsList(b.GetType()) && a is IList la && b is IList lb)
        {
            if (la.Count != lb.Count) return false;
            for (int i = 0; i < la.Count; i++)
                if (!ValuesEqual(la[i], lb[i])) return false;
            return true;
        }
        if (IsDict(a.GetType()) && IsDict(b.GetType()) && a is IDictionary da && b is IDictionary db)
        {
            if (da.Count != db.Count) return false;
            foreach (DictionaryEntry e in da)
            {
                if (e.Key == null || !db.Contains(e.Key)) return false;
                if (!ValuesEqual(e.Value, db[e.Key])) return false;
            }
            return true;
        }
        return Equals(a, b);
    }

    static object? CloneForAssign(object? src)
    {
        if (src == null) return null;
        Type t = src.GetType();
        if (t.IsValueType || t.IsEnum || t == typeof(string) || src is ImpAsset) return src;
        if (IsList(t) && src is IList from_list)
        {
            IList to = (IList)Activator.CreateInstance(t)!;
            for (int i = 0; i < from_list.Count; i++)
                to.Add(CloneForAssign(from_list[i]));
            return to;
        }
        if (IsDict(t) && src is IDictionary from_dict)
        {
            IDictionary to = (IDictionary)Activator.CreateInstance(t)!;
            foreach (DictionaryEntry e in from_dict)
                if (e.Key != null)
                    to[CloneForAssign(e.Key)!] = CloneForAssign(e.Value);
            return to;
        }
        return src;
    }

    static bool IsList(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
    static bool IsDict(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
    static bool IsTClass(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TClass<>);
    static bool IsTRef(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TRef<>);

    static bool IsExpandableStruct(Type t)
    {
        if (!t.IsValueType || t.IsPrimitive || t.IsEnum) return false;
        if (IsTClass(t) || IsTRef(t)) return false;
        return t != typeof(Color)
            && t != typeof(Vector2)
            && t != typeof(Vector3)
            && t != typeof(Vector4)
            && t != typeof(TLabel);
    }

    static bool IsSimpleEditor(Type t)
    {
        return t == typeof(int) || t == typeof(float) || t == typeof(bool) || t == typeof(string)
            || t == typeof(Color) || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4)
            || t == typeof(TLabel) || t.IsEnum;
    }

    static void ReserveTools(Action? tools)
    {
        if (tools == null) ImGui.SetNextItemWidth(-1);
        else ImGui.SetNextItemWidth(MathF.Max(40f, ImGui.GetContentRegionAvail().X - 88f));
    }

    static void DrawTools(Action? tools)
    {
        if (tools == null) return;
        ImGui.SameLine();
        tools();
    }

    bool DrawStructRow(string label, Type t, ref object? value, Action<object?>? apply, Action? tools = null,
        bool can_revert = false, object? revert = null)
    {
        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)));

        ImGui.TableSetColumnIndex(0);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.90f, 0.72f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(1f, 1f, 1f, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1f, 1f, 1f, 0.16f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(1f, 1f, 1f, 0.22f));
        bool open = ImGui.TreeNodeEx(label,
            ImGuiTreeNodeFlags.DefaultOpen |
            ImGuiTreeNodeFlags.SpanAvailWidth |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.Framed);
        ImGui.PopStyleColor(4);

        ImGui.TableSetColumnIndex(1);
        if (tools != null) { tools(); ImGui.SameLine(); }
        ImGui.TextDisabled(t.Name);
        bool changed = DrawRevertCol(ref value, can_revert, revert, apply);
        if (changed)
        {
            if (open) ImGui.TreePop();
            return true;
        }
        if (!open) return false;

        if (value != null)
        {
            foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsInitOnly) continue;
                ImpVarAttribute? attr = field.GetCustomAttribute<ImpVarAttribute>(true);
                if (attr is { Hidden: true }) continue;

                object? fv = field.GetValue(value);
                string flabel = attr?.Name ?? field.GetCustomAttribute<TitleAttribute>()?.Name ?? field.Name;
                object boxed = value;
                object? fdef = can_revert && revert != null ? field.GetValue(revert) : null;

                ImGui.PushID(field.Name);
                if (DrawRow(flabel, field.FieldType, ref fv, v =>
                    {
                        field.SetValue(boxed, v);
                        apply?.Invoke(boxed);
                    }, null, can_revert, fdef))
                {
                    field.SetValue(value, fv);
                    apply?.Invoke(value);
                    changed = true;
                }
                ImGui.PopID();
            }
        }

        ImGui.TreePop();
        return changed;
    }

    bool DrawListRow(string label, Type list_type, ref object? value, Action<object?>? apply, Action? tools,
        bool can_revert = false, object? revert = null)
    {
        Type elem = list_type.GetGenericArguments()[0];
        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.55f, 0.72f, 0.95f, 0.10f)));
        ImGui.TableSetColumnIndex(0);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.78f, 0.88f, 1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.45f, 0.62f, 0.90f, 0.22f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.45f, 0.62f, 0.90f, 0.32f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.45f, 0.62f, 0.90f, 0.42f));
        bool open = ImGui.TreeNodeEx(label,
            ImGuiTreeNodeFlags.DefaultOpen |
            ImGuiTreeNodeFlags.SpanAvailWidth |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.Framed);
        ImGui.PopStyleColor(4);

        ImGui.TableSetColumnIndex(1);
        bool changed = false;
        if (value == null)
        {
            ImGui.TextDisabled("List<" + elem.Name + ">");
            ImGui.SameLine();
            if (ImGui.SmallButton("Create"))
            {
                value = Activator.CreateInstance(list_type);
                apply?.Invoke(value);
                changed = true;
            }
            DrawTools(tools);
            if (DrawRevertCol(ref value, can_revert, revert, apply)) changed = true;
            if (open) ImGui.TreePop();
            return changed;
        }

        IList list = (IList)value;
        ImGui.TextDisabled("List<" + elem.Name + ">");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(48);
        int n = list.Count;
        if (ImGui.DragInt("##count", ref n, 0.2f, 0, 1024))
        {
            n = Math.Clamp(n, 0, 1024);
            while (list.Count < n) list.Add(MakeDefault(elem));
            while (list.Count > n) list.RemoveAt(list.Count - 1);
            apply?.Invoke(list);
            changed = true;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("+"))
        {
            if (elem.IsAbstract && !typeof(ImpAsset).IsAssignableFrom(elem))
            {
                IList captured = list;
                EDLG_PickClass.Run(elem, t =>
                {
                    if (t == null || t.IsAbstract) return;
                    captured.Add(Activator.CreateInstance(t));
                    apply?.Invoke(captured);
                });
            }
            else
            {
                list.Add(MakeDefault(elem));
                apply?.Invoke(list);
                changed = true;
            }
        }
        DrawTools(tools);
        if (DrawRevertCol(ref value, can_revert, revert, apply))
        {
            if (open) ImGui.TreePop();
            return true;
        }
        if (!open) return changed;

        int remove = -1, move_from = -1, move_to = -1;
        for (int i = 0; i < list.Count; i++)
        {
            int idx = i;
            object? item = list[i];
            ImGui.PushID(i);
            if (DrawRow("[" + i + "]", elem, ref item, v =>
                {
                    list[idx] = v;
                    apply?.Invoke(list);
                }, () =>
                {
                    if (idx > 0)
                    {
                        if (ImGui.SmallButton("^")) { move_from = idx; move_to = idx - 1; }
                        ImGui.SameLine();
                    }
                    if (idx < list.Count - 1)
                    {
                        if (ImGui.SmallButton("v")) { move_from = idx; move_to = idx + 1; }
                        ImGui.SameLine();
                    }
                    if (ImGui.SmallButton("X")) remove = idx;
                }))
            {
                list[i] = item;
                apply?.Invoke(list);
                changed = true;
            }
            ImGui.PopID();
        }

        if (remove >= 0 && remove < list.Count)
        {
            list.RemoveAt(remove);
            apply?.Invoke(list);
            changed = true;
        }
        else if (move_from >= 0 && move_to >= 0 && move_from < list.Count && move_to < list.Count)
        {
            object? tmp = list[move_from];
            list.RemoveAt(move_from);
            list.Insert(move_to, tmp);
            apply?.Invoke(list);
            changed = true;
        }

        ImGui.TreePop();
        return changed;
    }

    bool DrawDictRow(string label, Type dict_type, ref object? value, Action<object?>? apply, Action? tools,
        bool can_revert = false, object? revert = null)
    {
        Type kt = dict_type.GetGenericArguments()[0];
        Type vt = dict_type.GetGenericArguments()[1];
        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.55f, 0.72f, 0.95f, 0.10f)));
        ImGui.TableSetColumnIndex(0);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.78f, 0.88f, 1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.45f, 0.62f, 0.90f, 0.22f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.45f, 0.62f, 0.90f, 0.32f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.45f, 0.62f, 0.90f, 0.42f));
        bool open = ImGui.TreeNodeEx(label,
            ImGuiTreeNodeFlags.DefaultOpen |
            ImGuiTreeNodeFlags.SpanAvailWidth |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.Framed);
        ImGui.PopStyleColor(4);

        ImGui.TableSetColumnIndex(1);
        bool changed = false;
        if (value == null)
        {
            ImGui.TextDisabled("Dict<" + kt.Name + ", " + vt.Name + ">");
            ImGui.SameLine();
            if (ImGui.SmallButton("Create"))
            {
                value = Activator.CreateInstance(dict_type);
                apply?.Invoke(value);
                changed = true;
            }
            DrawTools(tools);
            if (DrawRevertCol(ref value, can_revert, revert, apply)) changed = true;
            if (open) ImGui.TreePop();
            return changed;
        }

        IDictionary dict = (IDictionary)value;
        ImGui.TextDisabled("Dict<" + kt.Name + ", " + vt.Name + "> (" + dict.Count + ")");
        ImGui.SameLine();
        object? add_key = null;
        if (kt.IsEnum)
        {
            foreach (object ev in Enum.GetValues(kt))
            {
                if (!dict.Contains(ev)) { add_key = ev; break; }
            }
        }
        else if (kt == typeof(int))
        {
            int n = 0;
            while (dict.Contains(n)) n++;
            add_key = n;
        }
        else if (kt == typeof(string) || kt == typeof(TLabel))
        {
            string name = "New";
            int n = 1;
            while (dict.Contains(kt == typeof(TLabel) ? (object)TLabel.From(name) : name))
                name = "New " + (++n);
            add_key = kt == typeof(TLabel) ? TLabel.From(name) : name;
        }
        else
        {
            object? k = MakeDefault(kt);
            if (k != null && !dict.Contains(k)) add_key = k;
        }

        if (add_key == null) ImGui.BeginDisabled();
        if (ImGui.SmallButton("+") && add_key != null)
        {
            dict[add_key] = MakeDefault(vt);
            apply?.Invoke(dict);
            changed = true;
        }
        if (add_key == null) ImGui.EndDisabled();
        DrawTools(tools);
        if (DrawRevertCol(ref value, can_revert, revert, apply))
        {
            if (open) ImGui.TreePop();
            return true;
        }
        if (!open) return changed;

        List<object> keys = new();
        foreach (DictionaryEntry e in dict)
            if (e.Key != null) keys.Add(e.Key);

        object? remove_key = null;
        bool compact = IsSimpleEditor(kt) && IsSimpleEditor(vt);
        for (int i = 0; i < keys.Count; i++)
        {
            object key = keys[i];
            object? val = dict[key];
            ImGui.PushID(i);
            if (compact)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.SetNextItemWidth(-1);
                object? kedit = key;
                ImGui.PushID("k");
                if (DrawEditor(kt, ref kedit) && kedit != null && !Equals(kedit, key) && !dict.Contains(kedit))
                {
                    dict.Remove(key);
                    dict[kedit] = val;
                    key = kedit;
                    apply?.Invoke(dict);
                    changed = true;
                }
                ImGui.PopID();
                ImGui.TableSetColumnIndex(1);
                ImGui.SetNextItemWidth(MathF.Max(40f, ImGui.GetContentRegionAvail().X - 28f));
                ImGui.PushID("v");
                if (DrawEditor(vt, ref val))
                {
                    dict[key] = val;
                    apply?.Invoke(dict);
                    changed = true;
                }
                ImGui.PopID();
                ImGui.SameLine();
                if (ImGui.SmallButton("X")) remove_key = key;
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                bool eopen = ImGui.TreeNodeEx("[" + i + "]",
                    ImGuiTreeNodeFlags.DefaultOpen |
                    ImGuiTreeNodeFlags.SpanAvailWidth |
                    ImGuiTreeNodeFlags.FramePadding);
                ImGui.TableSetColumnIndex(1);
                if (ImGui.SmallButton("X")) remove_key = key;
                ImGui.SameLine();
                ImGui.TextDisabled(key + "");
                if (eopen)
                {
                    object? kedit = key;
                    if (DrawRow("Key", kt, ref kedit, v =>
                        {
                            if (v == null || Equals(v, key) || dict.Contains(v)) return;
                            object? keep = dict[key];
                            dict.Remove(key);
                            dict[v] = keep;
                            key = v;
                            apply?.Invoke(dict);
                        }))
                    {
                        if (kedit != null && !Equals(kedit, keys[i]) && !dict.Contains(kedit))
                        {
                            object? keep = dict.Contains(key) ? dict[key] : val;
                            if (dict.Contains(key)) dict.Remove(key);
                            dict[kedit] = keep;
                            key = kedit;
                            apply?.Invoke(dict);
                            changed = true;
                        }
                    }
                    if (DrawRow("Value", vt, ref val, v =>
                        {
                            dict[key] = v;
                            apply?.Invoke(dict);
                        }))
                    {
                        dict[key] = val;
                        apply?.Invoke(dict);
                        changed = true;
                    }
                    ImGui.TreePop();
                }
            }
            ImGui.PopID();
        }

        if (remove_key != null)
        {
            dict.Remove(remove_key);
            apply?.Invoke(dict);
            changed = true;
        }

        ImGui.TreePop();
        return changed;
    }

    static object? MakeDefault(Type t)
    {
        if (t == typeof(string)) return "";
        if (t == typeof(TLabel)) return TLabel.From("New");
        if (typeof(ImpAsset).IsAssignableFrom(t)) return null;
        if (t.IsValueType || !t.IsAbstract)
        {
            try { return Activator.CreateInstance(t); }
            catch { return null; }
        }
        return null;
    }

    bool DrawClassRow(string label, Type t, ref object? value, Action<object?>? apply, Action? tools = null,
        bool can_revert = false, object? revert = null)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);

        ImGui.TableSetColumnIndex(1);
        string class_name = "";
        if (value != null)
            class_name = t.GetField("class_name")?.GetValue(value) as string ?? "";
        Type? cls = TClass<object>.Resolve(class_name);
        string name = cls?.GetCustomAttribute<TitleAttribute>()?.Name
            ?? cls?.Name
            ?? (string.IsNullOrEmpty(class_name) ? "None" : class_name);
        Texture2D ico = cls != null ? TypeIcon(cls) : default;

        float tools_w = tools != null ? 88f : 0f;
        float w = MathF.Max(40f, ImGui.GetContentRegionAvail().X - tools_w);
        float h = ImGui.GetFrameHeight();
        bool clicked = ImGui.Button("##cls", new Vector2(w, h));

        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float pad = ImGui.GetStyle().FramePadding.X;
        float icon = ImGui.GetTextLineHeight();
        float cy = (min.Y + max.Y) * 0.5f;
        float x = min.X + pad;
        if (ico.Id != 0)
        {
            dl.AddImage((IntPtr)ico.Id, new Vector2(x, cy - icon * 0.5f), new Vector2(x + icon, cy + icon * 0.5f));
            x += icon + 4f;
        }
        uint col = ImGui.GetColorU32(cls == null && string.IsNullOrEmpty(class_name) ? ImGuiCol.TextDisabled : ImGuiCol.Text);
        dl.PushClipRect(min, max, true);
        dl.AddText(new Vector2(x, cy - icon * 0.5f), col, name);
        dl.PopClipRect();

        if (clicked)
        {
            Type root = t.GetGenericArguments()[0];
            Type slot = t;
            Action<object?>? set = apply;
            EDLG_PickClass.Run(root, picked =>
            {
                if (picked == null) return;
                set?.Invoke(Activator.CreateInstance(slot, picked));
            });
        }

        DrawTools(tools);
        return DrawRevertCol(ref value, can_revert, revert, apply);
    }

    bool DrawRefRow(string label, Type t, ref object? value, Action<object?>? apply, Action? tools = null,
        bool can_revert = false, object? revert = null)
    {
        Type inner = t.GetGenericArguments()[0];
        if (typeof(ImpAsset).IsAssignableFrom(inner))
        {
            object? asset = value != null ? t.GetMethod("Get")?.Invoke(value, null) : null;
            object? revert_asset = can_revert && revert != null ? t.GetMethod("Get")?.Invoke(revert, null) : null;
            object? next_ref = value;
            Action<object?>? set = apply;
            bool changed = DrawAssetRow(label, inner, ref asset, a =>
            {
                next_ref = a == null ? Activator.CreateInstance(t) : Activator.CreateInstance(t, a);
                set?.Invoke(next_ref);
            }, tools, can_revert, revert_asset);
            value = next_ref;
            return changed;
        }

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);

        ImGui.TableSetColumnIndex(1);
        ReserveTools(tools);
        string path = "";
        if (value != null)
            path = t.GetField("path")?.GetValue(value) as string ?? "";
        object? path_obj = path;
        bool edited = DrawEditor(typeof(string), ref path_obj);
        DrawTools(tools);
        object? resolved = value != null ? t.GetMethod("Get")?.Invoke(value, null) : null;
        if (resolved is ImpComp c)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(string.IsNullOrEmpty(c.name) ? c.GetType().Name : c.name);
        }
        if (DrawRevertCol(ref value, can_revert, revert, apply)) return true;
        if (!edited) return false;
        object? next = Activator.CreateInstance(t, path_obj as string ?? "");
        value = next;
        apply?.Invoke(next);
        return true;
    }

    bool DrawAssetRow(string label, Type slot, ref object? value, Action<object?>? apply, Action? tools = null,
        bool can_revert = false, object? revert = null)
    {
        ImpAsset? asset = value as ImpAsset;
        bool inline = asset != null && (asset.is_inlined
            || (string.IsNullOrEmpty(asset.filepath) && !GAsset.Builtin_Is(asset)));

        bool pick = false, create = false, change = false, clear = false, to_inline = false, save = false;
        void Ops()
        {
            ImGuiStylePtr st = ImGui.GetStyle();
            float h = ImGui.GetFrameHeight();
            float bw = ImGui.CalcTextSize("...").X + st.FramePadding.X * 2f;
            float sp = st.ItemSpacing.X;
            float tools_w = tools != null ? 88f : 0f;
            float avail = ImGui.GetContentRegionAvail().X;
            float ident_w = MathF.Max(h, avail - bw - sp - tools_w);
            Vector2 origin = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(ident_w, h));

            ImGui.SetCursorScreenPos(origin);
            if (asset == null)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.TextDisabled("None");
            }
            else
            {
                DrawAssetThumb(asset);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.PushClipRect(origin, new Vector2(origin.X + ident_w, origin.Y + h), true);
                string name = AssetName(asset);
                ImGui.TextUnformatted(name);
                string type = asset.GetType().Name;
                if (!string.Equals(name, type, StringComparison.Ordinal))
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled(type);
                }
                ImGui.PopClipRect();
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X + ident_w + sp, origin.Y));
            if (ImGui.SmallButton("..."))
                ImGui.OpenPopup("##asset_ops");
            if (ImGui.BeginPopup("##asset_ops"))
            {
                if (asset == null)
                {
                    if (ImGui.MenuItem("Select...")) pick = true;
                    if (ImGui.MenuItem("Create")) create = true;
                }
                else if (inline)
                {
                    if (ImGui.MenuItem("Save to file...")) save = true;
                    if (ImGui.MenuItem("Change...")) change = true;
                    if (ImGui.MenuItem("Clear")) clear = true;
                }
                else
                {
                    if (ImGui.MenuItem("Change...")) change = true;
                    if (ImGui.MenuItem("Clear")) clear = true;
                    if (ImGui.MenuItem("Convert to inline")) to_inline = true;
                }
                ImGui.EndPopup();
            }
            DrawTools(tools);
        }

        if (asset == null)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.TableSetColumnIndex(1);
            Ops();
            bool created = false;
            if (pick) EDLG_PickAsset.Run(slot, a => apply?.Invoke(a));
            if (create)
            {
                void Make(Type t)
                {
                    if (t == null || t.IsAbstract) return;
                    if (Activator.CreateInstance(t) is not ImpAsset a) return;
                    a.is_inlined = true;
                    a.filepath = "";
                    apply?.Invoke(a);
                }
                if (slot.IsAbstract || slot == typeof(ImpAsset))
                    EDLG_PickClass.Run(slot, t => { if (t != null) Make(t); });
                else
                {
                    Make(slot);
                    created = true;
                }
            }
            if (DrawRevertCol(ref value, can_revert, revert, apply)) created = true;
            return created;
        }

        if (!inline)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.TableSetColumnIndex(1);

            bool changed = false;
            Ops();
            if (change) EDLG_PickAsset.Run(slot, a => apply?.Invoke(a));
            if (clear)
            {
                value = null;
                apply?.Invoke(null);
                changed = true;
            }
            if (to_inline)
            {
                ImpAsset? clone = CloneInline(asset);
                if (clone != null)
                {
                    value = clone;
                    apply?.Invoke(clone);
                    changed = true;
                }
            }
            if (DrawRevertCol(ref value, can_revert, revert, apply)) changed = true;
            return changed;
        }

        Vector4 tint = AssetTint(asset.GetType());
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(tint.X, tint.Y, tint.Z, 0.10f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(tint.X, tint.Y, tint.Z, 0.16f));

        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(tint.X, tint.Y, tint.Z, 0.22f)));

        ImGui.TableSetColumnIndex(0);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(tint.X, tint.Y, tint.Z, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(tint.X, tint.Y, tint.Z, 0.60f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(tint.X, tint.Y, tint.Z, 0.70f));
        bool open = ImGui.TreeNodeEx(label,
            ImGuiTreeNodeFlags.DefaultOpen |
            ImGuiTreeNodeFlags.SpanAvailWidth |
            ImGuiTreeNodeFlags.FramePadding |
            ImGuiTreeNodeFlags.Framed);
        ImGui.PopStyleColor(4);
        Vector2 box_min = ImGui.GetItemRectMin();
        float box_bottom = ImGui.GetItemRectMax().Y;

        ImGui.TableSetColumnIndex(2);
        float box_right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;

        ImGui.TableSetColumnIndex(1);
        bool inline_changed = false;
        Ops();
        box_bottom = MathF.Max(box_bottom, ImGui.GetItemRectMax().Y);
        if (save)
        {
            ImpAsset save_target = asset;
            EDLG_FileAction.SaveAsset(save_target, _ => apply?.Invoke(save_target));
        }
        if (change) EDLG_PickAsset.Run(slot, a => apply?.Invoke(a));
        if (clear)
        {
            value = null;
            apply?.Invoke(null);
            inline_changed = true;
        }
        bool reverted = DrawRevertCol(ref value, can_revert, revert, apply);
        if (open && !reverted)
            DrawMembers(asset, false);
        if (open) ImGui.TreePop();
        box_bottom = MathF.Max(box_bottom, ImGui.GetItemRectMax().Y);

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector2 box_max = new(box_right, box_bottom);
        dl.AddRectFilled(box_min, box_max, ImGui.GetColorU32(new Vector4(tint.X, tint.Y, tint.Z, 0.07f)), 4f);
        dl.AddRect(box_min, box_max, ImGui.GetColorU32(new Vector4(tint.X, tint.Y, tint.Z, 0.55f)), 4f, 0, 1.5f);
        ImGui.PopStyleColor(2);

        return reverted || inline_changed;
    }

    void DrawAssetThumb(ImpAsset asset)
    {
        float h = ImGui.GetFrameHeight();
        Vector2 sz = new(h, h);
        if (ImGui.Button("##thumb", sz))
            WND_Assets.Open(asset);

        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector4 tint = AssetTint(asset.GetType());
        dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(tint.X, tint.Y, tint.Z, 0.85f)), 2f);

        if (asset is A_Texture tex)
        {
            if (tex.texture.Id == 0 && !string.IsNullOrEmpty(tex.sourcefile))
                tex.Source_Reimport();
            if (tex.texture.Id != 0)
            {
                Vector2 pad = new(2, 2);
                dl.AddImage((IntPtr)tex.texture.Id, min + pad, max - pad, new Vector2(0f, 1f), new Vector2(1f, 0f));
            }
        }
    }

    static string AssetName(ImpAsset asset)
    {
        IReadOnlyList<TBuiltin> all = GAsset.Builtin_All();
        for (int i = 0; i < all.Count; i++)
            if (ReferenceEquals(all[i].asset, asset) && !string.IsNullOrEmpty(all[i].name))
                return all[i].name;

        if (!string.IsNullOrEmpty(asset.filepath))
        {
            string p = asset.filepath.Replace('\\', '/');
            if (p.StartsWith("{builtin}/", StringComparison.OrdinalIgnoreCase))
            {
                string rest = p["{builtin}/".Length..];
                int dot = rest.LastIndexOf('.');
                return dot >= 0 ? rest[(dot + 1)..] : rest;
            }
            string file = Path.GetFileNameWithoutExtension(asset.filepath);
            if (!string.IsNullOrEmpty(file)) return file;
        }
        return asset.is_inlined ? "inline" : asset.GetType().Name;
    }

    static Vector4 AssetTint(Type t)
    {
        AssetColorAttribute? c = t.GetCustomAttribute<AssetColorAttribute>();
        if (c == null)
            return new Vector4(0.30f, 0.62f, 0.40f, 1f);
        return new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, 1f);
    }

    static ImpAsset? CloneInline(ImpAsset src)
    {
        ImpAsset? clone = Activator.CreateInstance(src.GetType()) as ImpAsset;
        if (clone == null) return null;
        clone.From_Table(src.To_Table());
        clone.is_inlined = true;
        clone.filepath = "";
        return clone;
    }

    static bool DrawEditor(Type t, ref object? value)
    {
        if (t == typeof(int))
        {
            int v = (int)value!;
            if (!ImGui.DragInt("##v", ref v)) return false;
            value = v;
            return true;
        }

        if (t == typeof(float))
        {
            float v = (float)value!;
            if (!ImGui.DragFloat("##v", ref v, 0.1f)) return false;
            value = v;
            return true;
        }

        if (t == typeof(bool))
        {
            bool v = (bool)value!;
            if (!ImGui.Checkbox("##v", ref v)) return false;
            value = v;
            return true;
        }

        if (t == typeof(string))
        {
            string v = (string?)value ?? "";
            if (!ImGui.InputText("##v", ref v, 512)) return false;
            value = v;
            return true;
        }

        if (t == typeof(TLabel))
        {
            string v = value is TLabel lab ? lab.Value : "";
            if (!ImGui.InputText("##v", ref v, 256)) return false;
            value = TLabel.From(v);
            return true;
        }

        if (t.IsEnum)
        {
            string[] names = Enum.GetNames(t);
            int current = Array.IndexOf(names, value!.ToString());
            if (current < 0) current = 0;
            if (!ImGui.Combo("##v", ref current, names, names.Length)) return false;
            value = Enum.Parse(t, names[current]);
            return true;
        }

        if (t == typeof(Color))
        {
            Color c = (Color)value!;
            Vector4 v = new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            if (!ImGui.ColorEdit4("##v", ref v)) return false;
            value = new Color(
                (byte)(v.X * 255f),
                (byte)(v.Y * 255f),
                (byte)(v.Z * 255f),
                (byte)(v.W * 255f));
            return true;
        }

        if (t == typeof(Vector2))
        {
            Vector2 v = (Vector2)value!;
            Span<float> axes = stackalloc float[] { v.X, v.Y };
            if (!DragAxes(axes)) return false;
            value = new Vector2(axes[0], axes[1]);
            return true;
        }

        if (t == typeof(Vector3))
        {
            Vector3 v = (Vector3)value!;
            Span<float> axes = stackalloc float[] { v.X, v.Y, v.Z };
            if (!DragAxes(axes)) return false;
            value = new Vector3(axes[0], axes[1], axes[2]);
            return true;
        }

        if (t == typeof(Vector4))
        {
            Vector4 v = (Vector4)value!;
            Span<float> axes = stackalloc float[] { v.X, v.Y, v.Z, v.W };
            if (!DragAxes(axes)) return false;
            value = new Vector4(axes[0], axes[1], axes[2], axes[3]);
            return true;
        }

        ImGui.TextDisabled(t.Name);
        return false;
    }

    // Unity-style axis drags with a colored strip on the left edge of each field.
    static bool DragAxes(Span<float> axes)
    {
        int count = axes.Length;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float total = ImGui.CalcItemWidth();
        float width = (total - spacing * (count - 1)) / count;
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            if (i > 0) ImGui.SameLine();
            ImGui.PushID(i);
            ImGui.SetNextItemWidth(width);

            float slot = axes[i];
            if (ImGui.DragFloat("##a", ref slot, 0.1f))
            {
                axes[i] = slot;
                changed = true;
            }

            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRectFilled(min, new Vector2(min.X + 3f, max.Y), AxisCols[i]);

            ImGui.PopID();
        }

        return changed;
    }
}
