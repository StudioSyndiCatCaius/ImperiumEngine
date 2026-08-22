using System.Collections;
using System.Numerics;
using System.Text;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Hidden = true)]
public class C2_CollectionEdit : Imp2D
{
    const float HeadH = 26f;
    const float BtnW = 52f;

    public C2_InspectorProperty host;
    public bool is_expanded = true;

    public C2_Button c_btn_add;
    public C2_Button c_btn_clear;
    public C2_List c_rows;

    string _fp = "\0";

    public C2_CollectionEdit()
    {
        cursor_filter = ECursorFilter.Hit;
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Start;
        layout.size = new Vector2(0, HeadH);
        layout.size_min = layout.size;

        c_btn_add = new C2_Button
        {
            text = "Add",
            text_style = UI_Text.LIGHT,
            on_click = Add,
            layout = new TLayout2
            {
                size = new Vector2(BtnW, 22),
                size_min = new Vector2(BtnW, 22),
            },
        };
        Child_Add(c_btn_add);

        c_btn_clear = new C2_Button
        {
            text = "Clear",
            text_style = UI_Text.LIGHT,
            on_click = Clear,
            layout = new TLayout2
            {
                size = new Vector2(BtnW, 22),
                size_min = new Vector2(BtnW, 22),
            },
        };
        Child_Add(c_btn_clear);

        c_rows = new C2_List
        {
            orentation = EUIOrentation.V,
            spacing = 2,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Start,
            },
        };
        Child_Add(c_rows);
    }

    Type ColType
    {
        get
        {
            if (host != null)
            {
                return host.value_type;
            }
            return typeof(object);
        }
    }

    bool IsDict => C2_Inspector.Type_IsDict(ColType);
    bool IsArray => ColType != null && ColType.IsArray;
    bool Locked => host != null && host.IsReadOnly;

    object Value
    {
        get
        {
            if (host != null)
            {
                return host.Value_Get();
            }
            return null;
        }
    }

    int Count_Of(object col)
    {
        if (col is IDictionary dict)
        {
            return dict.Count;
        }
        if (col is IList list)
        {
            return list.Count;
        }
        return 0;
    }

    void MutateEach(Func<object, object> transform)
    {
        if (host == null || Locked || transform == null)
        {
            return;
        }
        ImpUndo.Group_Begin(host.label);
        for (int i = 0; i < host.binds.Count; i++)
        {
            TPropertyBind b = host.binds[i];
            object next = transform(b.Get());
            ImpUndo.Bind_Set(new List<TPropertyBind> { b }, next, host.label);
        }
        ImpUndo.Group_End();
        host.owner?.on_property_changed?.Invoke(host);
        _fp = "\0";
    }

    void Add()
    {
        Type t = ColType;
        if (IsDict)
        {
            Type kT = t.GetGenericArguments()[0];
            Type vT = t.GetGenericArguments()[1];
            MutateEach(cur =>
            {
                IDictionary next = Collection_Clone(cur, t) as IDictionary;
                if (next == null)
                {
                    return cur;
                }
                object key = UniqueKey(next, kT);
                if (key == null)
                {
                    return cur;
                }
                next.Add(key, C2_Inspector.Value_Default(vT));
                return next;
            });
            is_expanded = true;
            return;
        }
        Type eT = C2_Inspector.Type_Element(t);
        object item = C2_Inspector.Value_Default(eT);
        if (IsArray)
        {
            MutateEach(cur =>
            {
                Array arr = cur as Array;
                int n = 0;
                if (arr != null)
                {
                    n = arr.Length;
                }
                Array next = Array.CreateInstance(eT, n + 1);
                if (arr != null && n > 0)
                {
                    Array.Copy(arr, next, n);
                }
                next.SetValue(item, n);
                return next;
            });
            is_expanded = true;
            return;
        }
        MutateEach(cur =>
        {
            IList next = Collection_Clone(cur, t) as IList;
            if (next == null)
            {
                return cur;
            }
            next.Add(item);
            return next;
        });
        is_expanded = true;
    }

    void Clear()
    {
        Type t = ColType;
        MutateEach(_ => Collection_Empty(t));
    }

    void RemoveAt(int index)
    {
        Type t = ColType;
        Type eT = C2_Inspector.Type_Element(t);
        if (IsArray)
        {
            MutateEach(cur =>
            {
                Array arr = cur as Array;
                if (arr == null)
                {
                    return Collection_Empty(t);
                }
                if (index < 0 || index >= arr.Length)
                {
                    return Collection_Clone(cur, t);
                }
                Array next = Array.CreateInstance(eT, arr.Length - 1);
                if (index > 0)
                {
                    Array.Copy(arr, 0, next, 0, index);
                }
                if (index < arr.Length - 1)
                {
                    Array.Copy(arr, index + 1, next, index, arr.Length - index - 1);
                }
                return next;
            });
            return;
        }
        MutateEach(cur =>
        {
            IList next = Collection_Clone(cur, t) as IList;
            if (next == null)
            {
                return cur;
            }
            if (index >= 0 && index < next.Count)
            {
                next.RemoveAt(index);
            }
            return next;
        });
    }

    void RemoveKey(object key)
    {
        Type t = ColType;
        MutateEach(cur =>
        {
            IDictionary next = Collection_Clone(cur, t) as IDictionary;
            if (next == null)
            {
                return cur;
            }
            if (key != null && next.Contains(key))
            {
                next.Remove(key);
            }
            return next;
        });
    }

    bool CanAdd(object col)
    {
        if (Locked)
        {
            return false;
        }
        if (!IsDict)
        {
            return true;
        }
        Type kT = ColType.GetGenericArguments()[0];
        IDictionary dict = col as IDictionary;
        if (dict == null)
        {
            dict = Collection_Clone(null, ColType) as IDictionary;
        }
        return UniqueKey(dict, kT) != null;
    }

    void Rows_Sync()
    {
        if (host == null)
        {
            return;
        }
        object col = Value;
        string fp = Fingerprint(col);
        if (fp == _fp)
        {
            return;
        }
        if (Child_IsBusy(this) && _fp != "\0")
        {
            return;
        }
        _fp = fp;
        c_rows.Child_RemoveAll();
        if (col == null)
        {
            return;
        }
        if (IsDict)
        {
            if (col is not IDictionary dict)
            {
                return;
            }
            Type kT = ColType.GetGenericArguments()[0];
            Type vT = ColType.GetGenericArguments()[1];
            foreach (DictionaryEntry e in dict)
            {
                C2_CollectionRow row = new(this, e.Key, kT, vT);
                c_rows.Child_Add(row);
            }
            return;
        }
        if (col is not IList list)
        {
            return;
        }
        Type eT = C2_Inspector.Type_Element(ColType);
        for (int i = 0; i < list.Count; i++)
        {
            int idx = i;
            C2_CollectionRow row = new(this, idx, eT);
            c_rows.Child_Add(row);
        }
    }

    string Fingerprint(object col)
    {
        if (col is IDictionary dict)
        {
            StringBuilder sb = new();
            sb.Append(dict.Count);
            foreach (DictionaryEntry e in dict)
            {
                sb.Append('\n');
                if (e.Key != null)
                {
                    sb.Append(e.Key);
                }
            }
            return sb.ToString();
        }
        if (col is IList list)
        {
            return "n" + list.Count;
        }
        return "";
    }

    static bool Child_IsBusy(Imp2D n)
    {
        if (n == null)
        {
            return false;
        }
        if (n is C2_TextEdit te && te.is_focused)
        {
            return true;
        }
        if (n is C2_Dropdown drop && drop.IsOpen)
        {
            return true;
        }
        if (n is C2_Slider sl && sl.IsBusy)
        {
            return true;
        }
        if (n is C2_VectorEdit ve && ve.IsBusy())
        {
            return true;
        }
        if (n is C2_ColorPicker cp && cp.IsOpen)
        {
            return true;
        }
        if (n is C2_Picker pk && pk.IsOpen)
        {
            return true;
        }
        for (int i = 0; i < n.children.Count; i++)
        {
            if (n.children[i] is Imp2D d && Child_IsBusy(d))
            {
                return true;
            }
        }
        return false;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Rows_Sync();

        TDimensions2 dim = Dimensions_Get();
        object col = Value;
        int n = Count_Of(col);
        bool locked = Locked;
        bool can_add = CanAdd(col);
        c_btn_add.is_visible = can_add;
        c_btn_clear.is_visible = !locked && n > 0;

        float right = 0;
        if (c_btn_clear.is_visible)
        {
            right += BtnW;
            c_btn_clear.transform.position = new Vector2(dim.size.X - right, 2);
            c_btn_clear.layout.orient_H = EUIViewportAlignment.Start;
            c_btn_clear.layout.orient_V = EUIViewportAlignment.Start;
            right += 4;
        }
        if (c_btn_add.is_visible)
        {
            right += BtnW;
            c_btn_add.transform.position = new Vector2(dim.size.X - right, 2);
            c_btn_add.layout.orient_H = EUIViewportAlignment.Start;
            c_btn_add.layout.orient_V = EUIViewportAlignment.Start;
        }

        float rows_h = 0;
        bool show_rows = is_expanded && c_rows.children.Count > 0;
        c_rows.is_visible = show_rows;
        if (show_rows)
        {
            c_rows.transform.position = new Vector2(10, HeadH + 2);
            for (int i = 0; i < c_rows.children.Count; i++)
            {
                if (c_rows.children[i] is Imp2D d && d.is_visible)
                {
                    rows_h += d.layout.size.Y + 2;
                }
            }
            c_rows.layout.size = new Vector2(MathF.Max(0, dim.size.X - 10), rows_h);
            c_rows.layout.orient_H = EUIViewportAlignment.Start;
            c_rows.layout.orient_V = EUIViewportAlignment.Start;
        }

        float h = HeadH;
        if (rows_h > 0)
        {
            h = HeadH + rows_h + 4;
        }
        layout.size = new Vector2(layout.size.X, h);
        layout.size_min = new Vector2(layout.size_min.X, layout.size.Y);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        TDimensions2 dim = Dimensions_Get();
        UI_Box.BkgMid.Draw(new TDimensions2 { position = dim.position, size = new Vector2(dim.size.X, HeadH) });

        float cx = dim.position.X + 8;
        float cy = dim.position.Y + HeadH * 0.5f;
        Color ac = Color.White;
        if (is_expanded)
        {
            Raylib.DrawTriangle(new Vector2(cx - 4, cy - 3), new Vector2(cx + 4, cy - 3), new Vector2(cx, cy + 4), ac);
        }
        else
        {
            Raylib.DrawTriangle(new Vector2(cx - 3, cy - 5), new Vector2(cx - 3, cy + 5), new Vector2(cx + 5, cy), ac);
        }

        string title = host != null ? host.label : "";
        int n = Count_Of(Value);
        title = title + " (" + n + ")";
        UI_Text.LIGHT.Draw(title, new Vector2(dim.position.X + 16, dim.position.Y),
            new Vector2(MathF.Max(0, dim.size.X * 0.5f - 16), HeadH),
            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);

        if (!c_rows.is_visible)
        {
            float lx = dim.position.X + dim.size.X * 0.5f;
            string empty = n == 0 ? "None" : n.ToString();
            UI_Text.MUTED.Draw(empty, new Vector2(lx, dim.position.Y),
                new Vector2(MathF.Max(0, dim.size.X - lx - BtnW * 2 - 12), HeadH),
                0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);
        }

        base.OnDraw2D(dt, flags);
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt != ECursorEvent.Select_A)
        {
            return;
        }
        TDimensions2 dim = Dimensions_Get();
        if (player.cursor.position.Y > dim.position.Y + HeadH)
        {
            return;
        }
        if (player.cursor.position.X > dim.position.X + dim.size.X * 0.5f)
        {
            return;
        }
        is_expanded = !is_expanded;
    }

    public static object Collection_Clone(object src, Type declared)
    {
        if (declared == null)
        {
            return src;
        }
        if (declared.IsArray)
        {
            Type eT = declared.GetElementType();
            if (eT == null)
            {
                return src;
            }
            Array arr = src as Array;
            int n = 0;
            if (arr != null)
            {
                n = arr.Length;
            }
            Array copy = Array.CreateInstance(eT, n);
            if (arr != null && n > 0)
            {
                Array.Copy(arr, copy, n);
            }
            return copy;
        }
        if (C2_Inspector.Type_IsList(declared))
        {
            IList copy = Activator.CreateInstance(declared) as IList;
            if (copy == null)
            {
                return src;
            }
            if (src is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    copy.Add(list[i]);
                }
            }
            return copy;
        }
        if (C2_Inspector.Type_IsDict(declared))
        {
            IDictionary copy = Activator.CreateInstance(declared) as IDictionary;
            if (copy == null)
            {
                return src;
            }
            if (src is IDictionary dict)
            {
                foreach (DictionaryEntry e in dict)
                {
                    copy.Add(e.Key, e.Value);
                }
            }
            return copy;
        }
        return src;
    }

    public static object Collection_Empty(Type declared)
    {
        if (declared == null)
        {
            return null;
        }
        if (declared.IsArray)
        {
            Type eT = declared.GetElementType();
            if (eT == null)
            {
                return null;
            }
            return Array.CreateInstance(eT, 0);
        }
        try
        {
            return Activator.CreateInstance(declared);
        }
        catch
        {
            return null;
        }
    }

    public static object UniqueKey(IDictionary dict, Type kT)
    {
        if (kT == null)
        {
            return null;
        }
        if (kT.IsEnum)
        {
            Array vals = Enum.GetValues(kT);
            for (int i = 0; i < vals.Length; i++)
            {
                object v = vals.GetValue(i);
                if (dict == null || !dict.Contains(v))
                {
                    return v;
                }
            }
            return null;
        }
        if (kT == typeof(string))
        {
            int n = 0;
            string k = "Key";
            while (dict != null && dict.Contains(k))
            {
                n++;
                k = "Key " + n;
                if (n > 9999)
                {
                    return null;
                }
            }
            return k;
        }
        if (kT == typeof(TLabel))
        {
            int n = 0;
            TLabel k = TLabel.From("Key");
            while (dict != null && dict.Contains(k))
            {
                n++;
                k = TLabel.From("Key " + n);
                if (n > 9999)
                {
                    return null;
                }
            }
            return k;
        }
        if (kT == typeof(TTag))
        {
            int n = 0;
            TTag k = new TTag("Tag");
            while (dict != null && dict.Contains(k))
            {
                n++;
                k = new TTag("Tag " + n);
                if (n > 9999)
                {
                    return null;
                }
            }
            return k;
        }
        if (kT == typeof(int) || kT == typeof(uint) || kT == typeof(long) || kT == typeof(byte))
        {
            int n = 0;
            while (true)
            {
                object k = Convert.ChangeType(n, kT);
                if (dict == null || !dict.Contains(k))
                {
                    return k;
                }
                n++;
                if (n > 100000)
                {
                    return null;
                }
            }
        }
        object def = C2_Inspector.Value_Default(kT);
        if (def == null)
        {
            if (!kT.IsAbstract && kT.GetConstructor(Type.EmptyTypes) != null)
            {
                try
                {
                    def = Activator.CreateInstance(kT);
                }
                catch
                {
                    return null;
                }
            }
        }
        if (def == null)
        {
            return null;
        }
        if (dict == null || !dict.Contains(def))
        {
            return def;
        }
        return null;
    }

    internal void RequestRemoveIndex(int index)
    {
        RemoveAt(index);
    }

    internal void RequestRemoveKey(object key)
    {
        RemoveKey(key);
    }

    internal void Rekey(object from, object to)
    {
        if (host == null || Locked)
        {
            return;
        }
        if (TPropertyBind.Value_Same(from, to))
        {
            return;
        }
        if (to == null)
        {
            return;
        }
        for (int i = 0; i < host.binds.Count; i++)
        {
            TPropertyBind b = host.binds[i];
            object col = b.Get();
            if (col is not IDictionary dict)
            {
                continue;
            }
            if (from == null || !dict.Contains(from))
            {
                continue;
            }
            if (dict.Contains(to))
            {
                continue;
            }
            object val = dict[from];
            dict.Remove(from);
            dict.Add(to, val);
            b.Set(col);
        }
    }
}

[ImpClass(Hidden = true)]
class C2_CollectionRow : Imp2D
{
    const float RemoveW = 18f;

    readonly C2_CollectionEdit _owner;
    readonly int _index = -1;
    object _key;
    readonly bool _is_dict;

    C2_InspectorProperty _key_prop;
    C2_InspectorProperty _value_prop;
    C2_Button _remove;

    public C2_CollectionRow(C2_CollectionEdit owner, int index, Type element_type)
    {
        _owner = owner;
        _index = index;
        _is_dict = false;
        Setup(element_type, null);
        BuildList(element_type);
    }

    public C2_CollectionRow(C2_CollectionEdit owner, object key, Type key_type, Type value_type)
    {
        _owner = owner;
        _key = key;
        _is_dict = true;
        Setup(value_type, key_type);
        BuildDict(key_type, value_type);
    }

    void Setup(Type _, Type __)
    {
        cursor_filter = ECursorFilter.Pass;
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
        };
    }

    void BuildList(Type element_type)
    {
        C2_InspectorProperty host = _owner.host;
        List<TPropertyBind> binds = new();
        for (int i = 0; i < host.binds.Count; i++)
        {
            TPropertyBind b = TPropertyBind.Item(host.binds[i], _index, element_type);
            if (b != null)
            {
                binds.Add(b);
            }
        }
        _value_prop = MakeProp(binds, _index.ToString());
        Child_Add(_value_prop);
        BuildRemove(() => _owner.RequestRemoveIndex(_index));
    }

    void BuildDict(Type key_type, Type value_type)
    {
        C2_InspectorProperty host = _owner.host;
        List<TPropertyBind> key_binds = new();
        List<TPropertyBind> val_binds = new();
        for (int i = 0; i < host.binds.Count; i++)
        {
            TPropertyBind parent = host.binds[i];
            TPropertyBind kb = TPropertyBind.DictKey(parent, () => _key, RekeyTo, key_type);
            if (kb != null)
            {
                key_binds.Add(kb);
            }
            TPropertyBind vb = TPropertyBind.DictValue(parent, () => _key, value_type);
            if (vb != null)
            {
                val_binds.Add(vb);
            }
        }
        _key_prop = MakeProp(key_binds, "Key");
        _value_prop = MakeProp(val_binds, "Value");
        Child_Add(_key_prop);
        Child_Add(_value_prop);
        BuildRemove(() => _owner.RequestRemoveKey(_key));
    }

    void RekeyTo(object next)
    {
        object from = _key;
        _owner.Rekey(from, next);
        object col = _owner.host != null ? _owner.host.Value_Get() : null;
        if (col is not IDictionary dict)
        {
            return;
        }
        if (next != null && dict.Contains(next) && (from == null || !dict.Contains(from)))
        {
            _key = next;
        }
    }

    C2_InspectorProperty MakeProp(List<TPropertyBind> binds, string label)
    {
        C2_InspectorProperty row = new(_owner.host.owner, binds);
        row.label = label;
        row.Rebuild(_owner.host.depth + 1);
        return row;
    }

    void BuildRemove(Action on_remove)
    {
        if (_owner.host != null && _owner.host.IsReadOnly)
        {
            return;
        }
        _remove = new C2_Button
        {
            text = "x",
            text_style = UI_Text.MUTED,
            on_click = on_remove,
            layout = new TLayout2
            {
                size = new Vector2(RemoveW, RemoveW),
                size_min = new Vector2(RemoveW, RemoveW),
            },
        };
        Child_Add(_remove);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        TDimensions2 dim = Dimensions_Get();
        float remove_w = 0;
        if (_remove != null && _remove.is_visible)
        {
            remove_w = RemoveW + 4;
            _remove.transform.position = new Vector2(MathF.Max(0, dim.size.X - RemoveW - 2), 4);
            _remove.layout.orient_H = EUIViewportAlignment.Start;
            _remove.layout.orient_V = EUIViewportAlignment.Start;
        }

        float y = 0;
        float inner_w = MathF.Max(0, dim.size.X - remove_w);
        bool compact = _is_dict
            && _key_prop != null && _value_prop != null
            && _key_prop.c_group == null && !_key_prop.pedit_full_width
            && _value_prop.c_group == null && !_value_prop.pedit_full_width;
        if (compact)
        {
            float half = inner_w * 0.5f;
            _key_prop.transform.position = Vector2.Zero;
            _key_prop.layout.orient_H = EUIViewportAlignment.Start;
            _key_prop.layout.orient_V = EUIViewportAlignment.Start;
            _key_prop.layout.size = new Vector2(half, MathF.Max(26f, _key_prop.layout.size.Y));
            _value_prop.transform.position = new Vector2(half, 0);
            _value_prop.layout.orient_H = EUIViewportAlignment.Start;
            _value_prop.layout.orient_V = EUIViewportAlignment.Start;
            _value_prop.layout.size = new Vector2(MathF.Max(0, inner_w - half), MathF.Max(26f, _value_prop.layout.size.Y));
            y = MathF.Max(_key_prop.layout.size.Y, _value_prop.layout.size.Y);
        }
        else
        {
            if (_key_prop != null)
            {
                _key_prop.transform.position = new Vector2(0, y);
                _key_prop.layout.orient_H = EUIViewportAlignment.Start;
                _key_prop.layout.orient_V = EUIViewportAlignment.Start;
                _key_prop.layout.size = new Vector2(inner_w, _key_prop.layout.size.Y);
                y += _key_prop.layout.size.Y + 2;
            }
            if (_value_prop != null)
            {
                _value_prop.transform.position = new Vector2(0, y);
                _value_prop.layout.orient_H = EUIViewportAlignment.Start;
                _value_prop.layout.orient_V = EUIViewportAlignment.Start;
                _value_prop.layout.size = new Vector2(inner_w, _value_prop.layout.size.Y);
                y += _value_prop.layout.size.Y;
            }
        }

        float h = MathF.Max(22, y);
        layout.size = new Vector2(layout.size.X, h);
        layout.size_min = new Vector2(layout.size_min.X, h);
    }
}
