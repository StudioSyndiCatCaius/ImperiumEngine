using System.Numerics;
using ImperiumEngine.Dialogs;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Hidden = true)]
public class C2_AssetSlot : Imp2D
{
    const float HeadH = 26f;
    const float BtnW = 62f;

    public Type asset_type = typeof(ImpAsset);
    public string label = "";
    public Func<ImpAsset> value_get;
    public Action<ImpAsset> value_set;
    public Func<ImpAsset, List<Imp2D>> rows_build;
    public bool is_expanded;

    public C2_Picker c_picker;
    public C2_Button c_btn_mode;
    public C2_List c_rows;

    ImpAsset _rows_for;
    bool _rows_built;

    public ImpAsset Value => value_get?.Invoke();
    bool IsInline => Value is { filepath.Length: 0 };
    bool CanExpand => Value != null && rows_build != null;

    public C2_AssetSlot()
    {
        cursor_filter = ECursorFilter.Hit;
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Start;
        layout.size = new Vector2(0, HeadH);
        layout.size_min = layout.size;

        c_picker = new C2_Picker
        {
            placeholder = "None",
            text_get = Label_Value,
            tint_get = () => ImpAsset.Color_ForType(Value?.GetType() ?? asset_type),
            on_cleared = () => Value_Set(null),
            drop_accepts = Drop_Accepts,
            on_dropped = path =>
            {
                if (Drop_Accepts(path))
                {
                    Value_Set(ImpAsset.Load(path));
                }
            },
            on_open = () =>
            {
                Dialog_AssetPicker.Run(asset_type,
                    path =>
                    {
                        if (string.IsNullOrEmpty(path))
                        {
                            Value_Set(null);
                        }
                        else
                        {
                            Value_Set(ImpAsset.Load(path));
                        }
                    },
                    current_path: Value?.filepath,
                    title: "Select " + C2_Tree.Class_DisplayName(asset_type));
            },
        };
        Child_Add(c_picker);

        c_btn_mode = new C2_Button
        {
            text = "New",
            text_style = UI_Text.LIGHT,
            on_click = Mode_Toggle,
            layout = new TLayout2
                {
                    size = new Vector2(BtnW, 22),
                    size_min = new Vector2(BtnW, 22),
                },
        };
        Child_Add(c_btn_mode);

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

    // Only assets this slot's type can hold, so a dropped texture can't land in a mesh slot.
    bool Drop_Accepts(string path)
    {
        ImpAsset dropped = ImpAsset.Load(path);
        return dropped != null && asset_type != null && asset_type.IsAssignableFrom(dropped.GetType());
    }

    void Value_Set(ImpAsset asset)
    {
        value_set?.Invoke(asset);
        _rows_built = false;
        _rows_for = null;
    }

    void Mode_Toggle()
    {
        ImpAsset current = Value;
        if (current == null)
        {
            if (asset_type.IsAbstract || Activator.CreateInstance(asset_type) is not ImpAsset made) return;
            Value_Set(made);
            is_expanded = true;
            return;
        }
        ImpAsset copy = current.Clone();
        if (copy == null) return;
        Value_Set(copy);
        is_expanded = true;
    }

    string Label_Value()
    {
        ImpAsset asset = Value;
        if (asset == null) return "";
        return string.IsNullOrEmpty(asset.filepath)
            ? $"{asset.GetType().Name} (inline)"
            : ImpAsset.Name_ForPath(asset.filepath);
    }

    void Rows_Sync()
    {
        ImpAsset asset = Value;
        if (_rows_built && ReferenceEquals(_rows_for, asset)) return;
        _rows_built = true;
        _rows_for = asset;
        c_rows.Child_RemoveAll();
        if (asset != null && rows_build != null)
            foreach (Imp2D row in rows_build(asset)) c_rows.Child_Add(row);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Rows_Sync();

        TDimensions2 dim = Dimensions_Get();
        float label_w = dim.size.X * 0.38f;
        bool show_mode = !IsInline && !(Value == null && asset_type.IsAbstract);
        c_btn_mode.is_visible = show_mode;
        c_btn_mode.text = Value == null ? "New" : "Inline";
        c_btn_mode.transform.position = new Vector2(dim.size.X - BtnW, 2);
        c_btn_mode.layout.orient_H = EUIViewportAlignment.Start;
        c_btn_mode.layout.orient_V = EUIViewportAlignment.Start;

        c_picker.transform.position = new Vector2(label_w, 2);
        c_picker.layout.size = new Vector2(MathF.Max(0, dim.size.X - label_w - (show_mode ? BtnW + 4 : 0)), 22);
        c_picker.layout.orient_H = EUIViewportAlignment.Start;
        c_picker.layout.orient_V = EUIViewportAlignment.Start;

        bool show_rows = is_expanded && CanExpand && c_rows.children.Count > 0;
        c_rows.is_visible = show_rows;
        float rows_h = 0;
        if (show_rows)
        {
            c_rows.transform.position = new Vector2(10, HeadH + 2);
            for (int i = 0; i < c_rows.children.Count; i++)
                if (c_rows.children[i] is Imp2D d && d.is_visible) rows_h += d.layout.size.Y + 2;
            c_rows.layout.size = new Vector2(MathF.Max(0, dim.size.X - 10), rows_h);
            c_rows.layout.orient_H = EUIViewportAlignment.Start;
            c_rows.layout.orient_V = EUIViewportAlignment.Start;
        }

        layout.size = new Vector2(layout.size.X, HeadH + (show_rows ? rows_h + 4 : 0));
        layout.size_min = new Vector2(layout.size_min.X, layout.size.Y);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        TDimensions2 dim = Dimensions_Get();
        UiStyle_Box.STYLE_BKG_MID.Draw(new TDimensions2 { position = dim.position, size = new Vector2(dim.size.X, HeadH) });
        if (CanExpand)
        {
            float cx = dim.position.X + 8;
            float cy = dim.position.Y + HeadH * 0.5f;
            Color ac = Color.White;
            if (is_expanded)
                Raylib.DrawTriangle(new Vector2(cx - 4, cy - 3), new Vector2(cx + 4, cy - 3), new Vector2(cx, cy + 4), ac);
            else
                Raylib.DrawTriangle(new Vector2(cx - 3, cy - 5), new Vector2(cx - 3, cy + 5), new Vector2(cx + 5, cy), ac);
        }
        UI_Text.LIGHT.Draw(label, new Vector2(dim.position.X + 16, dim.position.Y),
            new Vector2(MathF.Max(0, dim.size.X * 0.38f - 16), HeadH),
            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);
        base.OnDraw2D(dt, flags);
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt != ECursorEvent.Select_A || !CanExpand) return;
        TDimensions2 dim = Dimensions_Get();
        if (player.cursor.position.X > dim.position.X + dim.size.X * 0.38f) return;
        if (player.cursor.position.Y > dim.position.Y + HeadH) return;
        is_expanded = !is_expanded;
    }
}
