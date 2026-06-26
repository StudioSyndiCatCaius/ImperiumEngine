using System.Collections;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumEngine.Interfaces;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

[Title( "Property Editor")]
public class O2D_PropertyEdit : ImpComponent2D
{
    private List<ImpObject> _objects = new();
    public List<ImpObject> objects
    {
        get => _objects;
        set { _objects = value ?? new(); Objects_Rebuild(); }
    }

    public void Objects_Set(params ImpObject[] objs)
    {
        _objects = objs?.ToList() ?? new();
        Objects_Rebuild();
    }

    private readonly O2D_Tree _tree = new();

    public O2D_PropertyEdit()
    {
        size_flags_h      = ESizeFlags.ExpandFill;
        size_flags_v      = ESizeFlags.ExpandFill;
        _tree.Anchors_Preset(EAnchorPreset.FullRect);
        _tree.row_height  = 26f;
        _tree.label_ratio = 0.42f;
        Child_Add(_tree);
    }

    public void Objects_Rebuild()
    {
        _tree.roots.Clear();

        if (_objects.Count == 0) { _tree.Items_Refresh(); return; }

        var cats    = new Dictionary<string, (List<TreeItem> normal, List<TreeItem> adv)>(StringComparer.Ordinal);
        var targets = _objects.Cast<object>().ToList();
        Action rebuild = Objects_Rebuild;

        foreach (var m in _objects[0].GetType()
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m is FieldInfo or PropertyInfo)
            .Where(m => m.GetCustomAttribute<ExposedAttribute>() != null)
            .Where(m => _objects.All(obj => m.DeclaringType!.IsAssignableFrom(obj.GetType()))))
        {
            string cat = m.GetCustomAttribute<CategoryAttribute>()?.Name ?? "General";
            bool   adv = m.GetCustomAttribute<AdvancedDisplayAttribute>() != null;

            if (!cats.ContainsKey(cat)) cats[cat] = (new(), new());
            (adv ? cats[cat].adv : cats[cat].normal).Add(Row_Build(m, targets, rebuild));
        }

        foreach (var (catName, (normal, adv)) in cats)
        {
            var cat = new TreeItem { label = catName, is_header = true, expanded = true };
            cat.children.AddRange(normal);

            if (adv.Count > 0)
            {
                var advSec = new TreeItem { label = "Advanced", expanded = false };
                advSec.children.AddRange(adv);
                cat.children.Add(advSec);
            }

            _tree.roots.Add(cat);
        }

        _tree.Items_Refresh();
    }

    // -----------------------------------------------------------------------------------------

    private static TreeItem Row_Build(MemberInfo m, List<object> targets, Action rebuild)
    {
        string label = m.GetCustomAttribute<TitleAttribute>()?.Name ?? Name_Format(m.Name);
        var    type  = m is FieldInfo fi ? fi.FieldType : ((PropertyInfo)m).PropertyType;

        Func<object?> get = () =>
            m is FieldInfo f ? f.GetValue(targets[0]) : ((PropertyInfo)m).GetValue(targets[0]);

        Action<object?> set = v =>
        {
            foreach (var obj in targets)
            {
                if (m is FieldInfo f2) f2.SetValue(obj, v);
                else ((PropertyInfo)m).SetValue(obj, v);

                if (obj is ImpConfig cfg)
                    cfg.Savable_OnWrite(cfg.Config_GetSavePath());
            }
        };

        return Item_Build(label, type, get, set, rebuild);
    }

    // Unified dispatch: handles Dictionary<,>, List<T>, structs, and primitives.
    private static TreeItem Item_Build(string label, Type type, Func<object?> get, Action<object?> set, Action rebuild)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            return Dict_Expand(label, type, get, set, rebuild);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return List_Expand(label, type, get, set, rebuild);

        if (type.IsValueType && !type.IsPrimitive && !type.IsEnum && !s_simpleTypes.Contains(type))
            return Struct_Expand(label, type, get, set, rebuild);

        return new TreeItem { label = label, widget = Widget_For(type, get, set) };
    }

    // -----------------------------------------------------------------------------------------
    // List<T>
    // -----------------------------------------------------------------------------------------

    // Payload passed from a drag handle to the drop target row.
    private sealed record ListReorderPayload(int FromIndex, IList ListRef, Action<IList> Commit, Action Rebuild);

    private static TreeItem List_Expand(string label, Type listType, Func<object?> get, Action<object?> set, Action rebuild)
    {
        var elemType = listType.GetGenericArguments()[0];
        bool simple  = Type_IsSimple(elemType);
        var  list    = (get() as IList) ?? (IList)Activator.CreateInstance(listType)!;

        var addBtn = new O2D_Button
        {
            text                = "+ Add",
            custom_minimum_size = new Vector2(60, 22),
            OnPressed           = _ =>
            {
                var l = (get() as IList) ?? (IList)Activator.CreateInstance(listType)!;
                l.Add(List_DefaultValue(elemType));
                set(l);
                rebuild();
            }
        };

        var header = new TreeItem { label = $"{label}  ({list.Count})", widget = addBtn, expanded = true };

        for (int i = 0; i < list.Count; i++)
        {
            int idx = i;

            Func<object?>   elemGet = () => { var l = get() as IList; return l != null && idx < l.Count ? l[idx] : null; };
            Action<object?> elemSet = v  => { var l = get() as IList; if (l != null && idx < l.Count) { l[idx] = v; set(l); } };

            // Drag handle — payload carries enough to perform the reorder on drop.
            var handle = new O2D_DragHandle(new ListReorderPayload(idx, list, l => set(l), rebuild));

            // Drop: accept only payloads from this same list; reorder by insert-at semantics.
            Func<object?, bool> canAccept = p => p is ListReorderPayload lp && ReferenceEquals(lp.ListRef, get() as IList);
            Action<object?> onDrop = raw =>
            {
                if (raw is not ListReorderPayload p || !ReferenceEquals(p.ListRef, get() as IList)) return;
                if (p.FromIndex == idx) return;
                var l = get() as IList;
                if (l == null) return;
                var elem = l[p.FromIndex];
                l.RemoveAt(p.FromIndex);
                int insertAt = idx > p.FromIndex ? idx - 1 : idx;
                l.Insert(insertAt, elem);
                p.Commit(l);
                p.Rebuild();
            };

            TreeItem elemItem;
            if (simple)
            {
                var valueWidget = Widget_For(elemType, elemGet, elemSet);
                valueWidget.size_flags_h = ESizeFlags.ExpandFill;
                elemItem = new TreeItem
                {
                    label            = $"[{idx}]",
                    left_widget      = handle,
                    widget           = List_Controls(idx, get, set, rebuild, valueWidget),
                    drop_enabled     = true,
                    OnDropCanAccept  = canAccept,
                    OnDrop           = onDrop,
                };
            }
            else
            {
                elemItem             = Item_Build($"[{idx}]", elemType, elemGet, elemSet, rebuild);
                elemItem.left_widget = handle;
                elemItem.widget      = List_Controls(idx, get, set, rebuild);
                elemItem.drop_enabled    = true;
                elemItem.OnDropCanAccept = canAccept;
                elemItem.OnDrop          = onDrop;
            }
            header.children.Add(elemItem);
        }

        return header;
    }

    // Builds [value?] [×] for one list element (reordering is now handled by drag & drop).
    private static ImpComponent2D List_Controls(
        int idx, Func<object?> get, Action<object?> set, Action rebuild,
        ImpComponent2D? valueWidget = null)
    {
        var row = new ImpComponent2D
        {
            layout_mode         = ELayoutMode.Horizontal,
            separation          = 2f,
            size_flags_h        = ESizeFlags.ExpandFill,
            custom_minimum_size = new Vector2(0, 22),
        };

        if (valueWidget != null) row.Child_Add(valueWidget);

        row.Child_Add(new O2D_Button
        {
            //text                = "×",
            icon = ImpAsset.Load<A_Texture2D>("T_ico_delete"),
            custom_minimum_size = new Vector2(24, 22),
            OnPressed           = _ =>
            {
                var l = get() as IList;
                if (l != null && idx < l.Count) { l.RemoveAt(idx); set(l); rebuild(); }
            }
        });

        return row;
    }

    private static object? List_DefaultValue(Type type)
    {
        if (type == typeof(string)) return "";
        try { return Activator.CreateInstance(type); } catch { return null; }
    }

    private static bool Type_IsSimple(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || s_simpleTypes.Contains(type);

    // -----------------------------------------------------------------------------------------
    // Dictionary<TKey, TValue>
    // -----------------------------------------------------------------------------------------

    private static TreeItem Dict_Expand(string label, Type dictType, Func<object?> get, Action<object?> set, Action rebuild)
    {
        var typeArgs  = dictType.GetGenericArguments();
        var keyType   = typeArgs[0];
        var valType   = typeArgs[1];
        bool simpleVal = Type_IsSimple(valType);

        var dict = (get() as IDictionary) ?? (IDictionary)Activator.CreateInstance(dictType)!;

        var addBtn = new O2D_Button
        {
            text                = "+ Add",
            custom_minimum_size = new Vector2(60, 22),
            OnPressed           = _ =>
            {
                var d      = (get() as IDictionary) ?? (IDictionary)Activator.CreateInstance(dictType)!;
                var newKey = Dict_DefaultKey(keyType, d);
                if (newKey == null || d.Contains(newKey)) return;
                d[newKey] = List_DefaultValue(valType);
                set(d);
                rebuild();
            }
        };

        var header = new TreeItem { label = $"{label}  ({dict.Count})", widget = addBtn, expanded = true };

        foreach (DictionaryEntry entry in dict)
        {
            var key    = entry.Key;     // captured per-iteration
            var keyStr = key.ToString() ?? "";

            Func<object?>   valGet = () => { var d = get() as IDictionary; return d != null && d.Contains(key) ? d[key] : null; };
            Action<object?> valSet = v  => { var d = get() as IDictionary; if (d != null && d.Contains(key)) { d[key] = v; set(d); } };

            var removeBtn = new O2D_Button
            {
                text                = "×",
                icon = ImpAsset.Load<A_Texture2D>("T_ico_delete"),
                custom_minimum_size = new Vector2(24, 22),
                OnPressed           = _ =>
                {
                    var d = get() as IDictionary;
                    if (d != null) { d.Remove(key); set(d); rebuild(); }
                }
            };

            TreeItem entryItem;
            if (simpleVal)
            {
                var valWidget = Widget_For(valType, valGet, valSet);
                valWidget.size_flags_h = ESizeFlags.ExpandFill;

                var row = new ImpComponent2D
                {
                    layout_mode         = ELayoutMode.Horizontal,
                    separation          = 2f,
                    size_flags_h        = ESizeFlags.ExpandFill,
                    custom_minimum_size = new Vector2(0, 22),
                };
                row.Child_Add(valWidget);
                row.Child_Add(removeBtn);

                entryItem = new TreeItem { label = keyStr, widget = row };
            }
            else
            {
                entryItem        = Item_Build(keyStr, valType, valGet, valSet, rebuild);
                entryItem.widget = removeBtn;
            }

            header.children.Add(entryItem);
        }

        return header;
    }

    // Generates a key that does not already exist in the dictionary.
    private static object? Dict_DefaultKey(Type keyType, IDictionary existing)
    {
        if (keyType == typeof(string))
        {
            string k = "new_key";
            int n = 0;
            while (existing.Contains(k)) k = $"new_key_{++n}";
            return k;
        }
        if (keyType == typeof(int))
        {
            int k = 0;
            foreach (DictionaryEntry e in existing)
                if (e.Key is int ik && ik >= k) k = ik + 1;
            return k;
        }
        if (keyType.IsEnum)
        {
            foreach (var v in Enum.GetValues(keyType))
                if (!existing.Contains(v)) return v;
            return null; // all values already used
        }
        try { return Activator.CreateInstance(keyType); } catch { return null; }
    }

    // -----------------------------------------------------------------------------------------
    // Struct expansion
    // -----------------------------------------------------------------------------------------

    private static readonly HashSet<Type> s_simpleTypes = new()
    {
        typeof(Color), typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion),
    };

    private static TreeItem Struct_Expand(string label, Type type, Func<object?> get, Action<object?> set, Action rebuild)
    {
        var item = new TreeItem { label = label, expanded = false };

        if (typeof(I_PropertyType).IsAssignableFrom(type))
        {
            var ctx = new PropertyEditorContext
            {
                GetValue    = get,
                Commit      = set,
                Row         = (rowLabel, widget) => item.children.Add(new TreeItem { label = rowLabel, widget = widget }),
                EmitDefault = () => Sub_Fields_Add(item, type, get, set, rebuild),
            };
            var temp = get() ?? Activator.CreateInstance(type);
            if (temp is I_PropertyType pt) pt.BuildPropertyEditor(ctx);
        }
        else
        {
            Sub_Fields_Add(item, type, get, set, rebuild);
        }

        return item;
    }

    private static void Sub_Fields_Add(TreeItem item, Type type, Func<object?> get, Action<object?> set, Action rebuild)
    {
        foreach (var sub in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var sf     = sub;
            var subGet = () => { var v = get(); return v != null ? sf.GetValue(v) : null; };
            var subSet = (object? sv) =>
            {
                var boxed = get() ?? Activator.CreateInstance(sf.DeclaringType!);
                sf.SetValue(boxed, sv);
                set(boxed);
            };
            item.children.Add(Item_Build(Name_Format(sub.Name), sub.FieldType, subGet, subSet, rebuild));
        }
    }

    // -----------------------------------------------------------------------------------------

    internal static ImpComponent2D Widget_For(Type type, Func<object?> get, Action<object?> set)
    {
        if (type == typeof(bool))    return new O2DPropWidgetBool(get, set);
        if (type == typeof(int))     return new O2DPropWidgetInt(get, set);
        if (type == typeof(float))   return new O2DPropWidgetFloat(get, set);
        if (type == typeof(double))  return new O2DPropWidgetDouble(get, set);
        if (type == typeof(string))  return new O2DPropWidgetStr(get, set);
        if (type == typeof(Color))   return new O2DPropWidgetColor(get, set);
        if (type == typeof(Vector2))     return new PropWidget_Vec2(get, set);
        if (type == typeof(Vector3))     return new PropWidget_Vec3(get, set);
        if (type == typeof(Quaternion))  return new PropWidget_Quat(get, set);
        if (type.IsEnum)             return new O2DPropWidgetEnum(type, get, set);
        return new PropWidget_Label(get);
    }

    private static string Name_Format(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i == 0) { sb.Append(char.ToUpper(c)); continue; }
            if (char.IsUpper(c) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
