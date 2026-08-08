using System.Numerics;
using System.Reflection;
using System.Text;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// ==========================================================================================
// Binding
// ==========================================================================================

// One editable member on one target. The inspector never touches fields directly - every
// read and write goes through a bind, which is what lets a field inside a struct work:
// structs copy on read, so the nested bind mutates a boxed copy and pushes the whole
// struct back through its parent.
public class TPropertyBind
{
    public readonly string name;
    public readonly Type type;

    readonly Func<object?> fn_get;
    readonly Action<object?> fn_set;

    public TPropertyBind(string name, Type type, Func<object?> get, Action<object?> set)
    {
        this.name = name;
        this.type = type;
        fn_get = get;
        fn_set = set;
    }

    public object? Get() => fn_get();
    public void Set(object? value) => fn_set(value);

    // Binds a member held directly by a target instance.
    public static TPropertyBind? Member(object target, MemberInfo m)
    {
        if (!Accessors(m, out var type, out var get, out var set)) return null;

        return new TPropertyBind(m.Name, type, () => get(target), v => set(target, v));
    }

    // Binds a member of the struct that `parent` points at.
    public static TPropertyBind? Nested(TPropertyBind parent, MemberInfo m)
    {
        if (!Accessors(m, out var type, out var get, out var set)) return null;

        return new TPropertyBind(m.Name, type,
            () =>
            {
                var box = parent.Get();
                return box == null ? null : get(box);
            },
            v =>
            {
                var box = parent.Get();
                if (box == null) return;

                set(box, v);
                parent.Set(box); //unbox back into the owner, or the edit is lost
            });
    }

    static bool Accessors(MemberInfo m, out Type type, out Func<object, object?> get, out Action<object, object?> set)
    {
        switch (m)
        {
            case FieldInfo f when !f.IsInitOnly && !f.IsLiteral:
                type = f.FieldType;
                get = o => f.GetValue(o);
                set = (o, v) => f.SetValue(o, v);
                return true;

            case PropertyInfo p when p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0:
                type = p.PropertyType;
                get = o => p.GetValue(o);
                set = (o, v) => p.SetValue(o, v);
                return true;
        }

        type = typeof(object);
        get = null!;
        set = null!;
        return false;
    }
}

// ==========================================================================================
// Inspector
// ==========================================================================================

// Property inspector: reflects the [ImpVar] members of whatever is selected and builds a
// row per member. Rows are rebuilt only on Rebuild(); between rebuilds they poll their
// bind each frame so external changes to the object show up without a full teardown.
public class C2_Inspector : ImpComp2D
{
    public List<object> selected_objects = new List<object>();
    [ImpVar] public bool allow_multi_select = true;

    //share of the row width given to the label column
    [ImpVar] public float label_ratio = 0.4f;
    //how deep nested structs are expanded before the inspector stops recursing
    [ImpVar] public int max_depth = 4;
    //group rows under a header per category; off lays every row out in one flat list
    [ImpVar] public bool use_categories = true;

    public Action<C2_InspectorProperty>? on_property_changed;

    public C2_ScrollBox c_scroll;
    public C2_List c_list;

    public C2_Inspector()
    {
        name = "Inspector";
        cursor_filter = ECursorFilter.Pass; //container only; rows and widgets take the cursor

        c_list = new C2_List
        {
            name = "Properties",
            Alignment = EUIAlignment.Vertical,
            separation = 2f,
            cursor_filter = ECursorFilter.Pass,
        };

        c_scroll = new C2_ScrollBox
        {
            name = "Scroll",
            anchor_preset = EUIAnchorPreset.Full,
        };

        c_scroll.Child_Add(c_list);
        Child_Add(c_scroll);
    }

    // ---------------------------------------------------
    // selection
    // ---------------------------------------------------

    public void Select(params object[] objects)
    {
        selected_objects.Clear();

        foreach (var o in objects)
        {
            if (o == null) continue;
            selected_objects.Add(o);
            if (!allow_multi_select) break;
        }

        Rebuild();
    }

    public void Select_Clear()
    {
        selected_objects.Clear();
        Rebuild();
    }

    // ---------------------------------------------------
    // build
    // ---------------------------------------------------

    public void Rebuild()
    {
        c_list.Child_RemoveAll();

        var targets = Targets_Get();
        if (targets.Count == 0)
        {
            c_list.Child_Add(new C2_Text("Nothing selected")
            {
                style_dim = true,
                align = 0.5f,
                cursor_filter = ECursorFilter.Ignore,
                size = new Vector2(0, Theme_Get().item_height),
            });
            return;
        }

        var members = Members_Shared(targets);

        if (!use_categories)
        {
            foreach (var m in members)
            {
                var flat = Row_Build(targets, m);
                if (flat != null) c_list.Child_Add(flat);
            }
            return;
        }

        foreach (var (category, list) in Categories_Group(members))
        {
            var box = new C2_Expandable
            {
                name = category,
                //class names are shown as declared: "ImpComp3D" prettifies to "Imp Comp3 D"
                title = category,
                is_expanded = true,
                icon = Category_Icon(category, list[0]),
            };

            foreach (var m in list)
            {
                var row = Row_Build(targets, m);
                if (row != null) box.Child_Add(row);
            }

            if (box.children.Count == 0) continue;
            c_list.Child_Add(box);
        }
    }

    // One member, bound across every selected target, as a row ready to be parented.
    C2_InspectorProperty? Row_Build(List<object> targets, MemberInfo m)
    {
        var binds = new List<TPropertyBind>();
        foreach (var t in targets)
        {
            var b = TPropertyBind.Member(t, Member_On(t.GetType(), m) ?? m);
            if (b != null) binds.Add(b);
        }

        if (binds.Count == 0) return null;

        var row = new C2_InspectorProperty(this, binds);
        row.Rebuild(0);
        return row;
    }

    // ---------------------------------------------------
    // categories
    // ---------------------------------------------------

    // A member's category: its [Category] when it declares one, otherwise the class that
    // declared it - so an inherited member files under the base class it actually came from
    // rather than whatever concrete type happens to be selected.
    public static string Category_Of(MemberInfo m)
    {
        var attr = m.GetCustomAttribute<CategoryAttribute>();
        if (attr?.Name is string custom && custom.Length > 0) return custom;

        return m.DeclaringType?.Name ?? "";
    }

    // Members bucketed by category, ordered by where each category first appears. Reflection
    // lists a type's own members ahead of the ones it inherits, so the selected object's own
    // class heads the inspector and its bases follow underneath.
    static List<KeyValuePair<string, List<MemberInfo>>> Categories_Group(List<MemberInfo> members)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<MemberInfo>>();

        foreach (var m in members)
        {
            string key = Category_Of(m);

            if (!map.TryGetValue(key, out var list))
            {
                list = new List<MemberInfo>();
                map[key] = list;
                order.Add(key);
            }

            list.Add(m);
        }

        var result = new List<KeyValuePair<string, List<MemberInfo>>>();
        foreach (var key in order) { result.Add(new KeyValuePair<string, List<MemberInfo>>(key, map[key])); }
        return result;
    }

    // A category named after the class it came from can use that class's icon, inheritance
    // fallback and all. A custom label has only its own name to match on.
    static A_Texture? Category_Icon(string category, MemberInfo first)
    {
        if (first.DeclaringType is Type t && t.Name == category) return ImpIcon.Get(t);
        return ImpIcon.Get(category);
    }

    // Skips nulls, and collapses to a single target when multi-select is off.
    List<object> Targets_Get()
    {
        var list = new List<object>();
        foreach (var o in selected_objects)
        {
            if (o == null) continue;
            list.Add(o);
            if (!allow_multi_select) break;
        }
        return list;
    }

    // Members every selected object has in common, by name and type. With one object
    // selected this is just its member list; with several it's the editable intersection.
    List<MemberInfo> Members_Shared(List<object> targets)
    {
        var shared = Members_Get(targets[0].GetType());
        if (targets.Count == 1) return shared;

        var result = new List<MemberInfo>();
        foreach (var m in shared)
        {
            bool on_all = true;
            for (int i = 1; i < targets.Count && on_all; i++)
            {
                on_all = Member_On(targets[i].GetType(), m) != null;
            }
            if (on_all) result.Add(m);
        }
        return result;
    }

    // Same-named member of the same type on another type, or null if it has none.
    static MemberInfo? Member_On(Type t, MemberInfo want)
    {
        foreach (var m in Members_Get(t))
        {
            if (m.Name == want.Name && Member_Type(m) == Member_Type(want)) return m;
        }
        return null;
    }

    // ---------------------------------------------------
    // reflection
    // ---------------------------------------------------

    static readonly Dictionary<Type, List<MemberInfo>> member_cache = new Dictionary<Type, List<MemberInfo>>();

    // Public instance members marked [ImpVar], base classes included. Cached: this runs
    // per row per rebuild and reflection lookups are not cheap.
    public static List<MemberInfo> Members_Get(Type t)
    {
        if (member_cache.TryGetValue(t, out var hit)) return hit;

        var list = new List<MemberInfo>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        foreach (var f in t.GetFields(flags))
        {
            if (f.GetCustomAttribute<ImpVarAttribute>() != null) list.Add(f);
        }
        foreach (var p in t.GetProperties(flags))
        {
            if (p.GetCustomAttribute<ImpVarAttribute>() != null) list.Add(p);
        }

        member_cache[t] = list;
        return list;
    }

    // Members to show inside an expanded struct. Engine structs tag their fields, but
    // library ones (Vector2, Color) don't - for those, fall back to every public field so
    // they still expand into something editable.
    public static List<MemberInfo> Members_GetNested(Type t)
    {
        var tagged = Members_Get(t);
        if (tagged.Count > 0) return tagged;

        var list = new List<MemberInfo>();
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!f.IsInitOnly && !f.IsLiteral) list.Add(f);
        }
        return list;
    }

    public static Type Member_Type(MemberInfo m)
    {
        return m switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => typeof(object),
        };
    }

    // "test_string" / "testString" -> "Test String"
    public static string Name_Pretty(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        var sb = new StringBuilder();
        bool next_upper = true;

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (c == '_' || c == ' ')
            {
                sb.Append(' ');
                next_upper = true;
                continue;
            }

            if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1])) sb.Append(' ');

            sb.Append(next_upper ? char.ToUpperInvariant(c) : c);
            next_upper = false;
        }

        return sb.ToString();
    }
}

// ==========================================================================================
// Property row
// ==========================================================================================

// One member: a label on the left and a type-appropriate editor on the right. Struct
// members instead become an expandable group holding a row per sub-member.
public class C2_InspectorProperty : ImpComp2D
{
    public C2_Inspector? _owner;
    public List<TPropertyBind> binds = new List<TPropertyBind>();
    public string label = "";

    public ImpComp2D? c_pedit; //editor widget, right column
    public C2_Text? c_label;
    public C2_Expandable? c_group; //set instead of c_pedit for struct members

    string[]? enum_names; //cached for the per-frame refresh, which would otherwise re-reflect

    //how many structs deep this row sits; I_Property implementations need it to keep nesting
    public int depth;

    //narrows the label column for rows whose editor needs the width more than the name does
    public float? label_ratio_override;

    public Type value_type => binds.Count > 0 ? binds[0].type : typeof(object);

    public C2_InspectorProperty() { }

    public C2_InspectorProperty(C2_Inspector? owner, List<TPropertyBind> binds)
    {
        _owner = owner;
        this.binds = binds;
        name = binds.Count > 0 ? binds[0].name : "";
        label = C2_Inspector.Name_Pretty(name);
    }

    // ---------------------------------------------------
    // value
    // ---------------------------------------------------

    public object? Value_Get() => binds.Count > 0 ? binds[0].Get() : null;

    public void Value_Set(object? new_value)
    {
        foreach (var b in binds) { b.Set(new_value); }
        _owner?.on_property_changed?.Invoke(this);
    }

    public void OnEdit(object? new_value) => Value_Set(new_value);

    // True when a multi-selection disagrees, so editors can show an indeterminate state.
    public bool Value_IsMixed()
    {
        if (binds.Count < 2) return false;

        var first = binds[0].Get();
        for (int i = 1; i < binds.Count; i++)
        {
            if (!Equals(first, binds[i].Get())) return true;
        }
        return false;
    }

    // ---------------------------------------------------
    // build
    // ---------------------------------------------------

    public void Rebuild(int depth = 0)
    {
        Child_RemoveAll();
        c_pedit = null;
        c_group = null;
        c_label = null;
        enum_names = null;
        label_ratio_override = null;
        this.depth = depth;

        cursor_filter = ECursorFilter.Pass;

        // a type can take over its own row entirely
        if (Value_Get() is I_Property custom && custom.Inspector_IsCustom())
        {
            custom.Inspector_Rebuild(this);
            return;
        }

        var t = value_type;

        // structs get a dropdown box with all their fields inside, unless they have an
        // editor of their own - a colour taken apart into R/G/B/A is four numbers nobody
        // can read as a colour, and a vector split over three rows is just noise
        if (Type_IsGroup(t) && !Type_HasEditor(t) && depth < (_owner?.max_depth ?? 4))
        {
            Group_Build(t, depth);
            return;
        }

        c_label = new C2_Text(label)
        {
            align = 0f,
            cursor_filter = ECursorFilter.Ignore,
        };
        Child_Add(c_label);

        c_pedit = Editor_Build(t);
        if (c_pedit != null) Child_Add(c_pedit);

        // A vector splits its column three or four ways, so the default share leaves each
        // field too narrow to print its own number. "Position" needs far less room than
        // three coordinates do.
        if (c_pedit is C2_VectorEdit vec) label_ratio_override = vec.Count >= 3 ? 0.28f : 0.34f;

        Refresh();
    }

    static bool Type_IsGroup(Type t)
    {
        if (!t.IsValueType || t.IsPrimitive || t.IsEnum) return false;
        if (t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan)) return false;

        return C2_Inspector.Members_GetNested(t).Count > 0;
    }

    // Structs Editor_Build knows how to draw whole. These are library types that can't
    // implement I_Property themselves, which is the difference between this list and the
    // interface - engine types declare their own custom rows.
    static bool Type_HasEditor(Type t)
    {
        return t == typeof(Color)
            || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4);
    }

    void Group_Build(Type t, int depth)
    {
        c_group = new C2_Expandable
        {
            name = name,
            title = label,
            is_expanded = true,
        };

        foreach (var m in C2_Inspector.Members_GetNested(t))
        {
            var nested = new List<TPropertyBind>();
            foreach (var b in binds)
            {
                var nb = TPropertyBind.Nested(b, m);
                if (nb != null) nested.Add(nb);
            }

            if (nested.Count == 0) continue;

            var row = new C2_InspectorProperty(_owner, nested);
            row.Rebuild(depth + 1);
            c_group.Child_Add(row);
        }

        Child_Add(c_group);
    }

    // Builds this row as a group holding just the named members, in the order given.
    //
    // This is the hook an I_Property struct uses from Inspector_Rebuild: it gets the same
    // nested binds the generic expansion uses - so writes still unbox back through the
    // owner - while deciding for itself which members show and in what order, rather than
    // taking whatever order reflection happens to return.
    public C2_Expandable Group_BuildNamed(params string[] member_names)
    {
        var t = value_type;

        c_group = new C2_Expandable
        {
            name = name,
            title = label,
            is_expanded = true,
        };

        foreach (var member_name in member_names)
        {
            var m = Member_Named(t, member_name);
            if (m == null) continue;

            var nested = new List<TPropertyBind>();
            foreach (var b in binds)
            {
                var nb = TPropertyBind.Nested(b, m);
                if (nb != null) nested.Add(nb);
            }

            if (nested.Count == 0) continue;

            var row = new C2_InspectorProperty(_owner, nested);
            row.Rebuild(depth + 1);
            c_group.Child_Add(row);
        }

        Child_Add(c_group);
        return c_group;
    }

    static MemberInfo? Member_Named(Type t, string member_name)
    {
        foreach (var m in C2_Inspector.Members_GetNested(t))
        {
            if (m.Name == member_name) return m;
        }
        return null;
    }

    // ---------------------------------------------------
    // editors
    // ---------------------------------------------------

    ImpComp2D? Editor_Build(Type t)
    {
        // BOOL -------------------
        if (t == typeof(bool)) return Editor_Bool();

        // ENUM -------------------
        if (t.IsEnum) return Editor_Enum(t);

        // NUMERIC -------------------
        if (Type_IsNumeric(t)) return Editor_Numeric(t);

        // STRING -------------------
        if (t == typeof(string)) return Editor_String();

        // COLOR -------------------
        if (t == typeof(Color)) return Editor_Color();

        // VECTOR -------------------
        if (t == typeof(Vector2)) return Editor_Vector(t, 2);
        if (t == typeof(Vector3)) return Editor_Vector(t, 3);
        if (t == typeof(Vector4)) return Editor_Vector(t, 4);

        // anything else has no editor yet; say so rather than drawing an empty slot
        return new C2_Text($"({t.Name})")
        {
            style_dim = true,
            align = 0f,
            cursor_filter = ECursorFilter.Ignore,
        };
    }

    C2_CheckBox Editor_Bool()
    {
        var box = new C2_CheckBox();
        box.on_toggled = b => Value_Set(b.is_checked);
        return box;
    }

    C2_Dropdown Editor_Enum(Type t)
    {
        enum_names = Enum.GetNames(t);

        var drop = new C2_Dropdown { placeholder_text = "-" };
        drop.Options_Set(enum_names);

        drop.on_dropdown_change = (_, opt, _) => Value_Set(Enum.Parse(t, opt.name));
        return drop;
    }

    // No range metadata exists yet, so numbers come up as unbounded drag-fields: press and
    // drag left/right to change the value, or click and type one in. Integers step by 1.
    C2_Progresser Editor_Numeric(Type t)
    {
        bool is_int = Type_IsInteger(t);

        var prog = new C2_Progresser
        {
            mouse_can_edit = true,
            can_type_edit = true,
            text_style = EProgresserTextStyle.Value,
            step_amount = is_int ? 1f : 0f,
            decimals = is_int ? 0 : 3,
            drag_sensitivity = is_int ? 0.1f : 0.01f,
        };

        prog.on_value_changed = p => Value_Set(Number_To(p.value, t));
        return prog;
    }

    C2_ColorPicker Editor_Color()
    {
        var picker = new C2_ColorPicker();
        picker.on_color_changed = p => Value_Set(p.color);
        return picker;
    }

    C2_VectorEdit Editor_Vector(Type t, int count)
    {
        var vec = new C2_VectorEdit(count);
        vec.on_changed = v => Value_Set(Vector_Pack(v, t));
        return vec;
    }

    // The two directions a vector crosses the editor. Vector4 doubles as a quaternion-free
    // catch-all; nothing needs W today but splitting the cases would cost more than it saves.
    static object Vector_Pack(C2_VectorEdit v, Type t)
    {
        if (t == typeof(Vector2)) return new Vector2(v.Value_Get(0), v.Value_Get(1));
        if (t == typeof(Vector3)) return new Vector3(v.Value_Get(0), v.Value_Get(1), v.Value_Get(2));
        return new Vector4(v.Value_Get(0), v.Value_Get(1), v.Value_Get(2), v.Value_Get(3));
    }

    static float[] Vector_Unpack(object? value)
    {
        return value switch
        {
            Vector2 v => new[] { v.X, v.Y },
            Vector3 v => new[] { v.X, v.Y, v.Z },
            Vector4 v => new[] { v.X, v.Y, v.Z, v.W },
            _ => Array.Empty<float>(),
        };
    }

    C2_TextEdit Editor_String()
    {
        var edit = new C2_TextEdit { placeholder_text = "..." };
        edit.on_text_changed = s => Value_Set(s);
        return edit;
    }

    static bool Type_IsNumeric(Type t) => Type_IsInteger(t) || t == typeof(float) || t == typeof(double);

    static bool Type_IsInteger(Type t)
    {
        return t == typeof(sbyte) || t == typeof(byte)
            || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint)
            || t == typeof(long) || t == typeof(ulong);
    }

    // Converts the progresser's float back to the member's own type, clamped to its range
    // so dragging a byte past 255 saturates instead of throwing.
    static object Number_To(float v, Type t)
    {
        if (t == typeof(float)) return v;
        if (t == typeof(double)) return (double)v;

        double d = Math.Round(v);
        double lo = Convert.ToDouble(t.GetField("MinValue")?.GetValue(null) ?? double.MinValue);
        double hi = Convert.ToDouble(t.GetField("MaxValue")?.GetValue(null) ?? double.MaxValue);

        return Convert.ChangeType(Math.Clamp(d, lo, hi), t);
    }

    // ---------------------------------------------------
    // refresh
    // ---------------------------------------------------

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Refresh();
    }

    // Pulls the current value into the editor widget. Skipped while the user is driving
    // that widget, so a refresh can't fight the edit in progress.
    public void Refresh()
    {
        if (c_pedit == null || binds.Count == 0 || Editor_IsBusy()) return;

        bool mixed = Value_IsMixed();
        var v = Value_Get();

        switch (c_pedit)
        {
            case C2_CheckBox box:
                box.is_checked = v is bool b && b;
                box.is_mixed = mixed;
                break;

            case C2_Dropdown drop:
                drop.current_option = mixed || v == null || enum_names == null
                    ? -1
                    : Array.IndexOf(enum_names, v.ToString());
                break;

            case C2_Progresser prog:
                prog.Value_SetQuiet(v == null ? 0f : Convert.ToSingle(v));
                prog.text_style = mixed ? EProgresserTextStyle.None : EProgresserTextStyle.Value;
                break;

            case C2_TextEdit edit:
                edit.Text_SetQuiet(mixed ? "" : v as string ?? "");
                edit.placeholder_text = mixed ? "-" : "...";
                break;

            case C2_ColorPicker picker:
                picker.is_mixed = mixed;
                if (v is Color color) picker.Color_SetQuiet(color);
                break;

            case C2_VectorEdit vec:
                vec.is_mixed = mixed;
                vec.Values_SetQuiet(mixed ? Array.Empty<float>() : Vector_Unpack(v));
                break;
        }
    }

    // An editor is off-limits while the user is working it. The ancestor walk is what makes
    // that true of the compound ones: the pointer is on a C2_Progresser inside a vector row,
    // or in the text box a progresser opened, never on the editor this row handed out.
    bool Editor_IsBusy()
    {
        if (Comp_IsWithin(ImpUI.focused, c_pedit) || Comp_IsWithin(ImpUI.pressed, c_pedit)) return true;

        // popups keep the cursor without holding focus, and outlive the click that opened them
        return c_pedit is C2_Dropdown d && d.IsOpen
            || c_pedit is C2_ColorPicker p && p.IsOpen;
    }

    static bool Comp_IsWithin(ImpComp? node, ImpComp? root)
    {
        if (root == null) return false;

        for (ImpComp? c = node; c != null; c = c.parent)
        {
            if (c == root) return true;
        }
        return false;
    }

    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    public override Vector2 Size_GetContentMin()
    {
        if (c_group != null) return c_group.Size_GetContentMin();
        return new Vector2(0, Theme_Get().item_height);
    }

    protected override void Layout_Children(Rectangle content)
    {
        if (c_group != null)
        {
            c_group.OnLayout_Exact(content);
            return;
        }

        float pad = Theme_Get().padding;
        float label_w = content.Width * (label_ratio_override ?? _owner?.label_ratio ?? 0.4f);

        c_label?.OnLayout_Exact(new Rectangle(
            content.X + pad, content.Y, MathF.Max(0, label_w - pad * 2), content.Height));

        c_pedit?.OnLayout_Exact(new Rectangle(
            content.X + label_w, content.Y, MathF.Max(0, content.Width - label_w), content.Height));
    }
}
