using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_AssetSlot : ImpComp2D
{
    const float HeadH = 26f;
    const float BtnW = 62f;

    public Type asset_type = typeof(ImpAsset);
    public string label = "";
    public Func<ImpAsset> value_get;
    public Action<ImpAsset> value_set;
    public Func<ImpAsset, List<ImpComp2D>> rows_build;
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
        view_alighnment_H = EUIViewportAlignment.Fill;
        view_alighnment_V = EUIViewportAlignment.Start;
        size = new Vector2(0, HeadH);
        size_min = size;

        c_picker = new C2_Picker
        {
            placeholder = "None",
            options_build = () => C2_Picker.Options_Assets(asset_type),
            text_get = Label_Value,
            tint_get = () => ImpAsset.Color_ForType(Value?.GetType() ?? asset_type),
            on_picked = opt => Value_Set(ImpAsset.Load(opt.data as string ?? "")),
            on_cleared = () => Value_Set(null),
            drop_accepts = Drop_Accepts,
            on_dropped = path =>
            {
                if (Drop_Accepts(path)) Value_Set(ImpAsset.Load(path));
            },
        };
        Child_Add(c_picker);

        c_btn_mode = new C2_Button
        {
            text = "New",
            text_style = UiStyle_Text.LIGHT,
            size = new Vector2(BtnW, 22),
            size_min = new Vector2(BtnW, 22),
            on_click = Mode_Toggle,
        };
        Child_Add(c_btn_mode);

        c_rows = new C2_List
        {
            alignment = EUIAlignment.Vertical,
            spacing = 2,
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Start,
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
            foreach (ImpComp2D row in rows_build(asset)) c_rows.Child_Add(row);
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
        c_btn_mode.view_alighnment_H = EUIViewportAlignment.Start;
        c_btn_mode.view_alighnment_V = EUIViewportAlignment.Start;

        c_picker.transform.position = new Vector2(label_w, 2);
        c_picker.size = new Vector2(MathF.Max(0, dim.size.X - label_w - (show_mode ? BtnW + 4 : 0)), 22);
        c_picker.view_alighnment_H = EUIViewportAlignment.Start;
        c_picker.view_alighnment_V = EUIViewportAlignment.Start;

        bool show_rows = is_expanded && CanExpand && c_rows.children.Count > 0;
        c_rows.is_visible = show_rows;
        float rows_h = 0;
        if (show_rows)
        {
            c_rows.transform.position = new Vector2(10, HeadH + 2);
            for (int i = 0; i < c_rows.children.Count; i++)
                if (c_rows.children[i] is ImpComp2D d && d.is_visible) rows_h += d.size.Y + 2;
            c_rows.size = new Vector2(MathF.Max(0, dim.size.X - 10), rows_h);
            c_rows.view_alighnment_H = EUIViewportAlignment.Start;
            c_rows.view_alighnment_V = EUIViewportAlignment.Start;
        }

        size.Y = HeadH + (show_rows ? rows_h + 4 : 0);
        size_min.Y = size.Y;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
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
        UiStyle_Text.LIGHT.Draw(label, new Vector2(dim.position.X + 16, dim.position.Y),
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
