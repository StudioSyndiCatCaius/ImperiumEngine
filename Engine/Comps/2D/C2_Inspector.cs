using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// One member on one target. Nested binds mutate a boxed struct copy and push it back
// through the parent, so edits inside a TTransform3 actually stick.
public class TPropertyBind
{
    public readonly string name;
    public readonly Type type;
    public readonly bool is_readonly;
    public readonly ImpVarAttribute var_attr;

    // What a freshly built target has in this member. Only meaningful when has_default is set -
    // a type we cannot construct simply has no yardstick and never offers a revert.
    public readonly bool has_default;
    public readonly object default_value;

    readonly Func<object> _get;
    readonly Action<object> _set;

    public TPropertyBind(string name, Type type, Func<object> get, Action<object> set, bool is_readonly = false,
        ImpVarAttribute var_attr = null, bool has_default = false, object default_value = null)
    {
        this.name = name;
        this.type = type;
        this.is_readonly = is_readonly;
        this.var_attr = var_attr;
        this.has_default = has_default;
        this.default_value = default_value;
        _get = get;
        _set = set;
    }

    public object Get() => _get();
    public void Set(object value) { if (!is_readonly) _set(value); }

    public bool IsDefault() => !has_default || Value_Same(Get(), default_value);

    // A null string and an empty one are the same edit as far as the user can see, and the string
    // editor writes "" where the field started null - without this every such row looks changed.
    public static bool Value_Same(object a, object b)
    {
        if (a is string || b is string)
            return string.Equals(a as string ?? "", b as string ?? "", StringComparison.Ordinal);
        return Equals(a, b);
    }

    public static TPropertyBind Member(object target, MemberInfo m)
    {
        if (!Accessors(m, out Type type, out var get, out var set, out bool locked, out ImpVarAttribute attr))
            return null;
        Default_Read(get, C2_Inspector.Default_Instance(target.GetType()), out bool has_def, out object def);
        return new TPropertyBind(m.Name, type, () => get(target), v => set(target, v), locked, attr, has_def, def);
    }

    public static TPropertyBind Nested(TPropertyBind parent, MemberInfo m)
    {
        if (!Accessors(m, out Type type, out var get, out var set, out bool locked, out ImpVarAttribute attr))
            return null;
        Default_Read(get, parent.has_default ? parent.default_value : null, out bool has_def, out object def);
        return new TPropertyBind(m.Name, type,
            () => { object box = parent.Get(); return box == null ? null : get(box); },
            v =>
            {
                object box = parent.Get();
                if (box == null) return;
                set(box, v);
                parent.Set(box);
            },
            locked || parent.is_readonly, attr, has_def, def);
    }

    static void Default_Read(Func<object, object> get, object owner, out bool has_default, out object value)
    {
        has_default = false;
        value = null;
        if (owner == null) return;
        try
        {
            value = get(owner);
            has_default = true;
        }
        catch { }
    }

    static bool Accessors(MemberInfo m, out Type type, out Func<object, object> get,
        out Action<object, object> set, out bool locked, out ImpVarAttribute attr)
    {
        attr = m.GetCustomAttribute<ImpVarAttribute>();
        locked = attr?.ReadOnly ?? false;
        switch (m)
        {
            case FieldInfo f when !f.IsLiteral:
                type = f.FieldType;
                get = o => f.GetValue(o);
                set = (o, v) => f.SetValue(o, v);
                locked |= f.IsInitOnly;
                return true;
            case PropertyInfo p when p.CanRead && p.GetIndexParameters().Length == 0:
                type = p.PropertyType;
                get = o => p.GetValue(o);
                set = (o, v) => p.SetValue(o, v);
                locked |= !p.CanWrite;
                return true;
        }
        type = typeof(object);
        get = null;
        set = null;
        locked = false;
        return false;
    }
}

// ##############################################################################
// INSPECTOR
// ##############################################################################

[ImpClass(Hidden = true)]
public class C2_Inspector : Imp2D
{
    public List<object> selected_objects = new();
    [ImpVar] public bool allow_multi_select = true;
    [ImpVar] public float label_ratio = 0.4f;
    [ImpVar] public int max_depth = 4;
    [ImpVar] public bool use_categories = true;
    [ImpVar] public bool show_advanced;
    [ImpVar] public bool declared_only;
    [ImpVar] public bool show_header = true;
    [ImpVar] public bool show_search = true;
    public float label_pad = 8f;
    public float depth_indent = 8f;

    public Action<C2_InspectorProperty> on_property_changed;
    public Action<ImpComp, ImpComp, ETreeDrop> on_hierarchy_drop;

    public C2_List list_properties = new()
    {
        orentation = EUIOrentation.V,
        is_scrollable = true,
        spacing = 2,
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
    };

    C2_List _header = new()
    {
        orentation = EUIOrentation.H,
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            size = new Vector2(0, 24),
            size_min = new Vector2(0, 24),
        },
        spacing = 4,
    };

    C2_CheckBox _check_advanced = new()
    {
        text = "Advanced",
        layout = new TLayout2
        {
            size = new Vector2(120, 22),
            size_min = new Vector2(80, 22),
        },
    };

    C2_SearchBar _search = new()
    {
        placeholder = "Search",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            size = new Vector2(0, 24),
            size_min = new Vector2(0, 24),
        },
    };

    string _search_query = "";

    UiStyle_Box style_box = UiStyle_Box.STYLE_BKG_DARK;
    readonly HashSet<string> _collapsed = new();
    static readonly Dictionary<Type, List<MemberInfo>> _member_cache = new();
    static readonly Dictionary<Type, I_Property> _prototypes = new();
    static readonly Dictionary<Type, object> _default_instances = new();
    static readonly HashSet<Type> _default_building = new();

    public C2_Inspector()
    {
        cursor_filter = ECursorFilter.Pass;
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;

        _check_advanced.on_changed = next =>
        {
            show_advanced = next;
            Rebuild();
        };
        _search.on_search = q =>
        {
            _search_query = q ?? "";
            Rebuild();
        };

        C2_List body = new()
        {
            orentation = EUIOrentation.V,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
            spacing = 2,
        };
        _header.Child_Add(_check_advanced);
        body.Child_Add(_search);
        body.Child_Add(_header);
        body.Child_Add(list_properties);
        Child_Add(body);
    }

    public void Object_Add(object obj, bool added)
    {
        if (added) selected_objects.Add(obj);
        else selected_objects.Remove(obj);
        Rebuild();
    }

    public void Objects_Add(List<object> objs, bool added, bool clear_first = true)
    {
        if (clear_first) selected_objects.Clear();
        if (objs != null)
        {
            foreach (object o in objs)
            {
                if (added) selected_objects.Add(o);
                else selected_objects.Remove(o);
            }
        }
        Rebuild();
    }

    public void Objects_Clear()
    {
        selected_objects.Clear();
        Rebuild();
    }

    public void Select(params object[] objects)
    {
        selected_objects.Clear();
        foreach (object o in objects)
        {
            if (o == null) continue;
            selected_objects.Add(o);
            if (!allow_multi_select) break;
        }
        Rebuild();
    }

    public void Properties_Rebuild() => Rebuild();

    public void Rebuild()
    {
        _check_advanced.is_checked = show_advanced;
        _header.is_visible = show_header;
        _search.is_visible = show_search;
        if (list_properties.scroll_box != null) list_properties.scroll_box.Child_RemoveAll();
        else list_properties.Child_RemoveAll();

        List<object> targets = Targets();
        if (targets.Count == 0)
        {
            AddRow(new C2_Text
            {
                text = "Nothing selected",
                style = UI_Text.MUTED,
                wrap = ETextWrap.None,
                layout = new TLayout2
                    {
                        orient_H = EUIViewportAlignment.Fill,
                        size = new Vector2(0, 22),
                        size_min = new Vector2(0, 22),
                    },
        });
            return;
        }

        List<MemberInfo> members = Members_Filter(Members_Shared(targets));
        if (declared_only && targets.Count > 0)
        {
            Type declared = targets[0].GetType();
            List<MemberInfo> cut = new();
            foreach (MemberInfo m in members)
                if (m.DeclaringType == declared) cut.Add(m);
            members = cut;
        }

        bool searching = !string.IsNullOrWhiteSpace(_search_query);
        if (members.Count == 0 && searching)
        {
            AddRow(new C2_Text
            {
                text = "No matching properties",
                style = UI_Text.MUTED,
                wrap = ETextWrap.None,
                layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Fill,
                    size = new Vector2(0, 22),
                    size_min = new Vector2(0, 22),
                },
            });
            return;
        }

        if (!use_categories)
        {
            foreach (MemberInfo m in members)
            {
                C2_InspectorProperty row = Row_Build(targets, m);
                if (row != null) AddRow(row);
            }
            return;
        }
        foreach (var (category, list) in Categories_Group(members))
        {
            Type cat_type = Category_Type(category, list);
            C2_Expandable box = new()
            {
                name = category,
                icon = C2_Tree.Class_Icon(cat_type),
                is_expanded = searching || !_collapsed.Contains(category),
                bar_height = 22,
                content_indent = 10f,
                layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Fill,
                    orient_V = EUIViewportAlignment.Start,
                },
            };
            box.on_expand = open =>
            {
                if (open) _collapsed.Remove(category);
                else _collapsed.Add(category);
            };

            float h = box.bar_height;
            foreach (MemberInfo m in list)
            {
                C2_InspectorProperty row = Row_Build(targets, m);
                if (row == null) continue;
                box.Child_Add(row);
                h += row.layout.size.Y + 2;
            }
            box.layout.size = new Vector2(0, h);
            box.layout.size_min = box.layout.size;
            AddRow(box);
        }

        if (!searching && targets.Count == 1 && targets[0] is ImpComp host
            && host.children.Count > 0 && !host.IsInstanceRoot && !host.IsPackedForeign)
            AddChildrenTree(host);
    }

    void AddChildrenTree(ImpComp host)
    {
        const string key = "Children";
        C2_Expandable box = new()
        {
            name = key,
            icon = C2_Tree.Class_Icon(typeof(ImpComp)),
            is_expanded = !_collapsed.Contains(key),
            bar_height = 22,
            content_indent = 10f,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Start,
            },
        };
        box.on_expand = open =>
        {
            if (open) _collapsed.Remove(key);
            else _collapsed.Add(key);
        };

        int count = 1 + ChildCount(host);
        float h = Math.Clamp(22f * count + 8f, 48f, 240f);
        C2_Tree tree = new()
        {
            allow_reorder = true,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                size = new Vector2(0, h),
                size_min = new Vector2(0, 48),
            },
        };
        tree.on_item_drop = (src, dst, where) =>
        {
            if (src.data is ImpComp a && dst.data is ImpComp b)
                on_hierarchy_drop?.Invoke(a, b, where);
        };
        tree.Tree_Populate_FromComp(host);
        box.Child_Add(tree);
        box.layout.size = new Vector2(0, box.bar_height + h + 4);
        box.layout.size_min = box.layout.size;
        AddRow(box);
    }

    static int ChildCount(ImpComp c)
    {
        int n = c.children.Count;
        for (int i = 0; i < c.children.Count; i++) n += ChildCount(c.children[i]);
        return n;
    }

    void AddRow(Imp2D row)
    {
        if (list_properties.scroll_box != null) list_properties.scroll_box.Child_Add(row);
        else list_properties.Child_Add(row);
    }

    C2_InspectorProperty Row_Build(List<object> targets, MemberInfo m)
    {
        List<TPropertyBind> binds = new();
        foreach (object t in targets)
        {
            TPropertyBind b = TPropertyBind.Member(t, Member_On(t.GetType(), m) ?? m);
            if (b != null) binds.Add(b);
        }
        if (binds.Count == 0) return null;
        C2_InspectorProperty row = new(this, binds);
        row.Rebuild(0);
        return row;
    }

    List<object> Targets()
    {
        List<object> list = new();
        foreach (object o in selected_objects)
        {
            if (o == null) continue;
            list.Add(o);
            if (!allow_multi_select) break;
        }
        return list;
    }

    public List<MemberInfo> Members_Filter(List<MemberInfo> members, bool search = true)
    {
        List<MemberInfo> list = new();
        foreach (MemberInfo m in members)
        {
            if (!show_advanced && m.GetCustomAttribute<ImpVarAttribute>()?.Advanced == true)
            {
                continue;
            }
            if (search && !Member_MatchesSearch(m))
            {
                continue;
            }
            list.Add(m);
        }
        return list;
    }

    bool Member_MatchesSearch(MemberInfo m)
    {
        string q = _search_query != null ? _search_query.Trim() : "";
        if (q.Length == 0)
        {
            return true;
        }
        return Member_Hits(m, q, 0);
    }

    bool Member_Hits(MemberInfo m, string q, int depth)
    {
        if (m == null)
        {
            return false;
        }
        if (m.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (Name_Pretty(m.Name).Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (depth == 0)
        {
            string cat = Category_Of(m);
            if (!string.IsNullOrEmpty(cat) && cat.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        if (depth >= max_depth)
        {
            return false;
        }
        Type t = Member_Type(m);
        if (t == null || t.IsPrimitive || t.IsEnum)
        {
            return false;
        }
        if (t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan))
        {
            return false;
        }
        if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Color))
        {
            return false;
        }
        List<MemberInfo> nested = Members_GetNested(t);
        for (int i = 0; i < nested.Count; i++)
        {
            if (nested[i] == m)
            {
                continue;
            }
            if (Member_Hits(nested[i], q, depth + 1))
            {
                return true;
            }
        }
        return false;
    }

    List<MemberInfo> Members_Shared(List<object> targets)
    {
        List<MemberInfo> shared = Members_Get(targets[0].GetType());
        if (targets.Count == 1) return shared;
        List<MemberInfo> result = new();
        foreach (MemberInfo m in shared)
        {
            bool on_all = true;
            for (int i = 1; i < targets.Count && on_all; i++)
                on_all = Member_On(targets[i].GetType(), m) != null;
            if (on_all) result.Add(m);
        }
        return result;
    }

    static MemberInfo Member_On(Type t, MemberInfo want)
    {
        foreach (MemberInfo m in Members_Get(t))
        {
            if (m.Name == want.Name && Member_Type(m) == Member_Type(want)) return m;
        }
        return null;
    }

    public static string Category_Of(MemberInfo m)
    {
        CategoryAttribute attr = m.GetCustomAttribute<CategoryAttribute>();
        if (!string.IsNullOrEmpty(attr?.Name)) return attr.Name;
        return m.DeclaringType?.Name ?? "";
    }

    static Type Category_Type(string category, List<MemberInfo> list)
    {
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Type d = list[i].DeclaringType;
                if (d == null)
                {
                    continue;
                }
                if (d.Name == category)
                {
                    return d;
                }
                if (C2_Tree.Class_DisplayName(d) == category)
                {
                    return d;
                }
            }
            if (list.Count > 0 && list[0].DeclaringType != null)
            {
                return list[0].DeclaringType;
            }
        }
        if (string.IsNullOrEmpty(category))
        {
            return null;
        }
        return ImpComp.Type_FromName(category);
    }

    static List<(string cat, List<MemberInfo> list)> Categories_Group(List<MemberInfo> members)
    {
        List<string> order = new();
        Dictionary<string, List<MemberInfo>> map = new();
        foreach (MemberInfo m in members)
        {
            string key = Category_Of(m);
            if (!map.TryGetValue(key, out List<MemberInfo> list))
            {
                list = new List<MemberInfo>();
                map[key] = list;
                order.Add(key);
            }
            list.Add(m);
        }
        List<(string, List<MemberInfo>)> result = new();
        foreach (string key in order) result.Add((key, map[key]));
        return result;
    }

    public static List<MemberInfo> Members_Get(Type t)
    {
        if (_member_cache.TryGetValue(t, out List<MemberInfo> hit)) return hit;
        List<MemberInfo> list = new();
        List<Type> chain = new();
        for (Type cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
            chain.Add(cur);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (Type cur in chain)
        {
            foreach (FieldInfo f in cur.GetFields(flags))
                if (f.GetCustomAttribute<ImpVarAttribute>() != null) list.Add(f);
            foreach (PropertyInfo p in cur.GetProperties(flags))
                if (p.GetCustomAttribute<ImpVarAttribute>() != null) list.Add(p);
        }
        _member_cache[t] = list;
        return list;
    }

    public static List<MemberInfo> Members_GetNested(Type t)
    {
        List<MemberInfo> tagged = Members_Get(t);
        if (tagged.Count > 0) return tagged;
        List<MemberInfo> list = new();
        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!f.IsInitOnly && !f.IsLiteral) list.Add(f);
        }
        return list;
    }

    public static Type Member_Type(MemberInfo m) => m switch
    {
        FieldInfo f => f.FieldType,
        PropertyInfo p => p.PropertyType,
        _ => typeof(object),
    };

    public static string Name_Pretty(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        StringBuilder sb = new();
        bool next_upper = true;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '_' || c == ' ') { sb.Append(' '); next_upper = true; continue; }
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
            sb.Append(next_upper ? char.ToUpperInvariant(c) : c);
            next_upper = false;
        }
        return sb.ToString();
    }

    /// <summary>
    /// A freshly built instance of a type, kept as the yardstick for "default value" - whatever the
    /// field initialisers and constructor left in a member is what the revert button puts back.
    /// Types without a usable parameterless constructor give null, and those rows never offer revert.
    /// </summary>
    internal static object Default_Instance(Type t)
    {
        if (_default_instances.TryGetValue(t, out object hit)) return hit;

        // A comp whose constructor points an inspector at itself - the file browser does exactly
        // that - would otherwise ask for its own default part way through building it, and never
        // stop. Anything re-entering for a type already under construction gets no default.
        if (!_default_building.Add(t)) return null;
        object made = null;
        try
        {
            if (!t.IsAbstract && (t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null))
                made = Activator.CreateInstance(t);
        }
        catch { }
        finally { _default_building.Remove(t); }

        _default_instances[t] = made;
        return made;
    }

    internal static I_Property Prototype(Type t)
    {
        if (_prototypes.TryGetValue(t, out I_Property hit)) return hit;
        I_Property made = null;
        if (!t.IsAbstract)
        {
            try { made = Activator.CreateInstance(t) as I_Property; }
            catch { }
        }
        _prototypes[t] = made;
        return made;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        style_box?.Draw(Dimensions_Get());
    }
}

// ##############################################################################
// PROPERTY
// ##############################################################################

[ImpClass(Hidden = true)]
public class C2_InspectorProperty : Imp2D
{
    public C2_Inspector owner;
    public List<TPropertyBind> binds = new();
    public string label = "";
    public Imp2D c_pedit;
    public C2_Text c_label;
    public C2_Expandable c_group;
    public C2_ButtonRevert c_revert;
    public float? label_ratio_override;
    public bool pedit_full_width;
    public int depth;

    string[] _enum_names;
    const float RowH = 26f;
    const float RevertW = 16f;

    public Type value_type => binds.Count > 0 ? binds[0].type : typeof(object);
    public bool IsReadOnly => binds.Count > 0 && binds[0].is_readonly;
    public bool Depth_CanNest => depth < (owner?.max_depth ?? 4);

    /// <summary>True when the row could ever revert - the gutter is reserved on those rows so the
    /// editor does not resize as the button comes and goes.</summary>
    public bool Revert_CanEver
    {
        get
        {
            if (IsReadOnly) return false;
            for (int i = 0; i < binds.Count; i++)
                if (binds[i].has_default) return true;
            return false;
        }
    }

    public bool IsModified()
    {
        for (int i = 0; i < binds.Count; i++)
            if (!binds[i].IsDefault()) return true;
        return false;
    }

    public C2_InspectorProperty() { }

    public C2_InspectorProperty(C2_Inspector owner, List<TPropertyBind> binds)
    {
        this.owner = owner;
        this.binds = binds;
        name = binds.Count > 0 ? binds[0].name : "";
        label = C2_Inspector.Name_Pretty(name);
        cursor_filter = ECursorFilter.Pass;
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.size = new Vector2(0, RowH);
        layout.size_min = layout.size;
    }

    public object Value_Get() => binds.Count > 0 ? binds[0].Get() : null;

    public void Value_Set(object value)
    {
        if (IsReadOnly) return;
        // The row itself is the merge key: a slider drag or a burst of typing on one row
        // collapses into a single undo step, while touching another row starts a new one.
        ImpUndo.Bind_Set(binds, value, label, this);
        owner?.on_property_changed?.Invoke(this);
    }

    /// <summary>
    /// Puts every bind back to its own default. Multi-select can hold targets of different types
    /// with different defaults, so each bind is set separately and the group makes it one undo step.
    /// </summary>
    public void Value_Revert()
    {
        if (IsReadOnly) return;
        ImpUndo.Group_Begin($"Revert {label}");
        foreach (TPropertyBind b in binds)
        {
            if (!b.has_default) continue;
            ImpUndo.Bind_Set(new List<TPropertyBind> { b }, b.default_value, label);
        }
        ImpUndo.Group_End();
        owner?.on_property_changed?.Invoke(this);
        Refresh();
    }

    public void Rebuild(int depth = 0)
    {
        Child_RemoveAll();
        c_pedit = null;
        c_group = null;
        c_label = null;
        c_revert = null;
        _enum_names = null;
        label_ratio_override = null;
        pedit_full_width = false;
        this.depth = depth;
        cursor_filter = ECursorFilter.Pass;

        Type t = value_type;
        I_Property custom = Property_Custom(t);
        if (custom != null)
        {
            custom.Inspector_Rebuild(this);
            if (IsReadOnly) ReadOnly_Apply();
            FitHeight();
            return;
        }

        if (Type_IsGroup(t) && !Type_HasEditor(t) && depth < (owner?.max_depth ?? 4))
        {
            Group_Build(t, depth);
            FitHeight();
            return;
        }

        Editor_Set(Editor_Build(t));
        if (c_pedit is C2_VectorEdit vec) label_ratio_override = vec.Count >= 3 ? 0.28f : 0.34f;
        if (IsReadOnly) ReadOnly_Apply();
        Refresh();
    }

    I_Property Property_Custom(Type t)
    {
        if (Value_Get() is I_Property live && live.Inspector_IsCustom()) return live;
        if (!typeof(I_Property).IsAssignableFrom(t)) return null;
        I_Property stand = C2_Inspector.Prototype(t);
        return stand != null && stand.Inspector_IsCustom() ? stand : null;
    }

    public void Editor_Set(Imp2D editor, float? label_ratio = null)
    {
        c_label = new C2_Text
        {
            text = label,
            style = UI_Text.LIGHT,
            wrap = ETextWrap.None,
            text_alignment_h = EUIPositionAlignment.Start,
            text_alignment_v = EUIPositionAlignment.Center,
            cursor_filter = ECursorFilter.Ignore,
        };
        Child_Add(c_label);
        c_pedit = editor;
        if (editor != null) Child_Add(editor);
        Revert_Build();
        label_ratio_override = label_ratio;
        layout.size = new Vector2(0, RowH);
        layout.size_min = layout.size;
    }

    void Revert_Build()
    {
        if (!Revert_CanEver) return;
        c_revert = new C2_ButtonRevert { is_visible = false };
        c_revert.on_click = Value_Revert;
        Child_Add(c_revert);
    }

    public void Editor_SetFull(Imp2D editor)
    {
        c_pedit = editor;
        pedit_full_width = true;
        if (editor != null) Child_Add(editor);
        layout.size = new Vector2(0, MathF.Max(RowH, editor?.layout.size.Y ?? RowH));
        layout.size_min = layout.size;
    }

    public List<Imp2D> Rows_ForObject(object target)
    {
        List<Imp2D> rows = new();
        if (target == null) return rows;
        List<MemberInfo> members = C2_Inspector.Members_Get(target.GetType());
        if (owner != null) members = owner.Members_Filter(members, false);
        foreach (MemberInfo m in members)
        {
            TPropertyBind bind = TPropertyBind.Member(target, m);
            if (bind == null) continue;
            C2_InspectorProperty row = new(owner, new List<TPropertyBind> { bind });
            row.Rebuild(depth + 1);
            rows.Add(row);
        }
        return rows;
    }

    public C2_Expandable Group_BuildNamed(params string[] member_names)
    {
        c_group = MakeGroup();
        foreach (string member_name in member_names)
        {
            MemberInfo m = Member_Named(value_type, member_name);
            if (m != null) AddNested(m);
        }
        Child_Add(c_group);
        FitHeight();
        return c_group;
    }

    void Group_Build(Type t, int depth)
    {
        c_group = MakeGroup();
        List<MemberInfo> members = C2_Inspector.Members_GetNested(t);
        if (owner != null) members = owner.Members_Filter(members, false);
        foreach (MemberInfo m in members) AddNested(m);
        Child_Add(c_group);
    }

    C2_Expandable MakeGroup()
    {
        return new C2_Expandable
        {
            name = label,
            is_expanded = true,
            bar_height = 22,
            content_indent = 10f + depth * 4f,
            layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Fill,
                    orient_V = EUIViewportAlignment.Start,
                    size = new Vector2(0, 22),
                    size_min = new Vector2(0, 22),
                },
        };
    }

    void AddNested(MemberInfo m)
    {
        List<TPropertyBind> nested = new();
        foreach (TPropertyBind b in binds)
        {
            TPropertyBind nb = TPropertyBind.Nested(b, m);
            if (nb != null) nested.Add(nb);
        }
        if (nested.Count == 0) return;
        C2_InspectorProperty row = new(owner, nested);
        row.Rebuild(depth + 1);
        c_group.Child_Add(row);
    }

    static MemberInfo Member_Named(Type t, string member_name)
    {
        foreach (MemberInfo m in C2_Inspector.Members_GetNested(t))
            if (m.Name == member_name) return m;
        return null;
    }

    static bool Type_IsGroup(Type t)
    {
        if (!t.IsValueType || t.IsPrimitive || t.IsEnum) return false;
        if (t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan)) return false;
        return C2_Inspector.Members_GetNested(t).Count > 0;
    }

    static bool Type_HasEditor(Type t) =>
        t == typeof(Color) || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4);

    Imp2D Editor_Build(Type t)
    {
        if (t == typeof(bool))
        {
            C2_CheckBox box = new();
            box.on_changed = v => Value_Set(v);
            return box;
        }
        if (t.IsEnum)
        {
            _enum_names = Enum.GetNames(t);
            C2_Dropdown drop = new() { placeholder_text = "-" };
            drop.Options_Set(_enum_names);
            drop.on_dropdown_change = (_, opt, _) => Value_Set(Enum.Parse(t, opt.name));
            return drop;
        }
        if (t == typeof(string))
        {
            C2_TextEdit edit = new() { text_placeholder = "..." };
            edit.on_text_changed = s => Value_Set(s ?? "");
            return edit;
        }
        if (Type_IsNumeric(t))
        {
            bool is_int = t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(byte);
            ImpVarAttribute attr = binds.Count > 0 ? binds[0].var_attr : null;
            bool ranged = attr != null && attr.Max > attr.Min;
            C2_Slider slider = new()
            {
                is_spinner = true,
                min = ranged ? attr.Min : 0f,
                max = ranged ? attr.Max : 0f,
                step = is_int ? 1f : 0f,
                value_text_decimals = is_int ? 0 : 3,
                drag_sensitivity = is_int ? 0.15f : 0.05f,
            };
            slider.on_changed = s =>
            {
                if (t == typeof(int)) Value_Set((int)Math.Clamp(MathF.Round(s.value), int.MinValue, int.MaxValue));
                else if (t == typeof(float)) Value_Set(s.value);
                else if (t == typeof(double)) Value_Set((double)s.value);
                else if (t == typeof(uint)) Value_Set((uint)Math.Max(0, MathF.Round(s.value)));
                else if (t == typeof(long)) Value_Set((long)MathF.Round(s.value));
                else if (t == typeof(byte)) Value_Set((byte)Math.Clamp(MathF.Round(s.value), 0, 255));
            };
            return slider;
        }
        if (t == typeof(Color))
        {
            C2_ColorPicker col = new();
            col.on_color_changed = c => Value_Set(c.color);
            return col;
        }
        if (t == typeof(Vector2)) return Editor_Vector(2);
        if (t == typeof(Vector3)) return Editor_Vector(3);
        if (t == typeof(Vector4)) return Editor_Vector(4);
        if (typeof(ImpAsset).IsAssignableFrom(t))
        {
            return new C2_Text
            {
                text = Value_Get() is ImpAsset a
                    ? (string.IsNullOrEmpty(a.filepath) ? a.GetType().Name : a.GetName())
                    : "None",
                style = UI_Text.LIGHT,
                text_alignment_h = EUIPositionAlignment.Start,
                wrap = ETextWrap.None,
            };
        }
        return new C2_Text
        {
            text = $"({t.Name})",
            style = UI_Text.MUTED,
            text_alignment_h = EUIPositionAlignment.Start,
            wrap = ETextWrap.None,
            cursor_filter = ECursorFilter.Ignore,
        };
    }

    C2_VectorEdit Editor_Vector(int count)
    {
        C2_VectorEdit vec = new(count);
        vec.on_changed = v =>
        {
            if (count == 2) Value_Set(new Vector2(v.Value_Get(0), v.Value_Get(1)));
            else if (count == 3) Value_Set(new Vector3(v.Value_Get(0), v.Value_Get(1), v.Value_Get(2)));
            else Value_Set(new Vector4(v.Value_Get(0), v.Value_Get(1), v.Value_Get(2), v.Value_Get(3)));
        };
        return vec;
    }

    static bool Type_IsNumeric(Type t) =>
        t == typeof(int) || t == typeof(float) || t == typeof(double)
        || t == typeof(uint) || t == typeof(long) || t == typeof(byte);

    void ReadOnly_Apply()
    {
        if (c_label != null) c_label.style = UI_Text.MUTED;
        if (c_pedit == null) return;
        c_pedit.cursor_filter = ECursorFilter.Ignore;
        if (c_pedit is C2_CheckBox box) box.is_disabled = true;
        if (c_pedit is C2_Button btn) btn.is_disabled = true;
    }

    void FitHeight()
    {
        if (c_group == null) return;
        float h = c_group.bar_height;
        void AddKids(List<ImpComp> kids)
        {
            for (int i = 0; i < kids.Count; i++)
            {
                if (kids[i] is C2_List or C2_Box or C2_Button) continue;
                if (kids[i] is Imp2D d && d.is_visible) h += d.layout.size.Y + 2;
            }
        }
        AddKids(c_group.children);
        if (c_group.content_box != null) AddKids(c_group.content_box.children);
        c_group.layout.size = new Vector2(0, h);
        c_group.layout.size_min = c_group.layout.size;
        layout.size = c_group.layout.size;
        layout.size_min = layout.size;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        TDimensions2 dim = Dimensions_Get();

        if (c_group != null)
        {
            float group_inset = depth * (owner?.depth_indent ?? 8f);
            c_group.layout.orient_H = group_inset > 0 ? EUIViewportAlignment.Start : EUIViewportAlignment.Fill;
            c_group.layout.orient_V = EUIViewportAlignment.Fill;
            if (group_inset > 0)
            {
                c_group.transform.position = new Vector2(group_inset, c_group.transform.position.Y);
                c_group.layout.size = new Vector2(MathF.Max(0, dim.size.X - group_inset), c_group.layout.size.Y);
            }
            if (c_group.layout.size.Y > 0)
            {
                layout.size = new Vector2(layout.size.X, c_group.layout.size.Y);
                layout.size_min = new Vector2(layout.size_min.X, layout.size.Y);
            }
            return;
        }

        if (pedit_full_width && c_pedit != null)
        {
            c_pedit.layout.orient_H = EUIViewportAlignment.Fill;
            c_pedit.layout.orient_V = EUIViewportAlignment.Start;
            layout.size = new Vector2(layout.size.X, MathF.Max(RowH, c_pedit.layout.size.Y));
            layout.size_min = new Vector2(layout.size_min.X, layout.size.Y);
            return;
        }

        float ratio = label_ratio_override ?? owner?.label_ratio ?? 0.4f;
        float pad = owner?.label_pad ?? 8f;
        float inset = pad + depth * (owner?.depth_indent ?? 8f);
        float lw = dim.size.X * ratio;
        float gutter = c_revert != null ? RevertW : 0f;
        if (c_label != null)
        {
            c_label.layout.size = new Vector2(MathF.Max(0, lw - inset), dim.size.Y);
            c_label.transform.position = new Vector2(inset, 0);
            c_label.layout.orient_H = EUIViewportAlignment.Start;
            c_label.layout.orient_V = EUIViewportAlignment.Fill;
        }
        if (c_pedit != null)
        {
            c_pedit.layout.size = new Vector2(MathF.Max(0, dim.size.X - lw - gutter), dim.size.Y);
            c_pedit.transform.position = new Vector2(lw, 0);
            c_pedit.layout.orient_H = EUIViewportAlignment.Start;
            c_pedit.layout.orient_V = EUIViewportAlignment.Fill;
        }
        if (c_revert != null)
        {
            c_revert.is_visible = IsModified();
            c_revert.layout.size = new Vector2(RevertW, MathF.Min(RevertW, dim.size.Y));
            c_revert.transform.position = new Vector2(
                MathF.Max(0, dim.size.X - RevertW), (dim.size.Y - c_revert.layout.size.Y) * 0.5f);
            c_revert.layout.orient_H = EUIViewportAlignment.Start;
            c_revert.layout.orient_V = EUIViewportAlignment.Start;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (binds.Count == 0 || Editor_IsBusy() || c_pedit == null) return;
        object v = Value_Get();
        switch (c_pedit)
        {
            case C2_CheckBox box:
                box.is_checked = v is bool b && b;
                break;
            case C2_Dropdown drop:
                drop.Option_SetQuiet(v == null || _enum_names == null ? -1 : Array.IndexOf(_enum_names, v.ToString()));
                break;
            case C2_TextEdit edit:
                if (!edit.is_focused)
                    edit.text = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
                break;
            case C2_Slider slider:
                if (!slider.IsBusy && v != null) slider.Value_SetQuiet(Convert.ToSingle(v));
                break;
            case C2_ColorPicker col:
                if (v is Color c) col.Color_SetQuiet(c);
                break;
            case C2_VectorEdit vec:
                vec.Values_SetQuiet(v switch
                {
                    Vector2 a => new[] { a.X, a.Y },
                    Vector3 n => new[] { n.X, n.Y, n.Z },
                    Vector4 q => new[] { q.X, q.Y, q.Z, q.W },
                    _ => Array.Empty<float>(),
                });
                break;
        }
    }

    bool Editor_IsBusy()
    {
        if (c_pedit is C2_TextEdit te && te.is_focused) return true;
        if (c_pedit is C2_Dropdown d && d.IsOpen) return true;
        if (c_pedit is C2_Slider sl && sl.IsBusy) return true;
        if (c_pedit is C2_VectorEdit ve && ve.IsBusy()) return true;
        if (c_pedit is C2_ColorPicker cp && cp.IsOpen) return true;
        if (c_pedit is C2_Picker pk && pk.IsOpen) return true;
        return false;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (c_group != null || pedit_full_width) return;
        TDimensions2 dim = Dimensions_Get();
        Raylib.DrawRectangleV(
            new Vector2(dim.position.X + 6, dim.position.Y + dim.size.Y - 1),
            new Vector2(MathF.Max(0, dim.size.X - 12), 1),
            new Color(255, 255, 255, 18));
    }
}

// ##############################################################################
// REVERT BUTTON
// ##############################################################################

/// <summary>
/// The little "put it back" arrow an inspector row shows while its value differs from the default.
/// The arrow is drawn rather than typed - the editor fonts only carry ASCII.
/// </summary>
public class C2_ButtonRevert : C2_Button
{
    static readonly UI_Button STYLE = new()
    {
        style_unhovered = new UiStyle_Box { texture = null, tint = new Color(0, 0, 0, 0) },
        style_hovered = UiStyle_Box.STYLE_BTN_HOVER,
        style_pressed = UiStyle_Box.STYLE_BTN_PRESS,
    };

    public Color icon_color = new(235, 195, 90, 255);

    public C2_ButtonRevert()
    {
        style = STYLE;
        content_pad = 2;
        layout.size = new Vector2(16, 16);
        layout.size_min = layout.size;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);

        TDimensions2 dim = Dimensions_Get();
        float r = MathF.Min(dim.size.X, dim.size.Y) * 0.32f;
        if (r < 2f) return;
        Vector2 c = dim.position + dim.size * 0.5f;
        float thick = MathF.Max(1.5f, r * 0.42f);

        const float start = 45f, end = 315f;
        Raylib.DrawRing(c, MathF.Max(0, r - thick), r, start, end, 20, icon_color);

        // Arrowhead sits on the open end of the ring, pointing back along the sweep.
        float rad = start * MathF.PI / 180f;
        Vector2 tip = c + new Vector2(MathF.Cos(rad), MathF.Sin(rad)) * (r - thick * 0.5f);
        Raylib.DrawPoly(tip, 3, thick * 1.8f, start - 90f, icon_color);
    }
}
