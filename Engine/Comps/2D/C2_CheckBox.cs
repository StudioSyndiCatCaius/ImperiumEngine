using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_CheckBox : Imp2D
{
    [ImpVar] public bool is_checked = false;
    [ImpVar] public string text = "";
    [ImpVar] public UiStyle_CheckBox style = UiStyle_CheckBox.DEFAULT;
    [ImpVar] public UI_Text Text = UI_Text.LIGHT;
    [ImpVar] public float box_size = 16;
    [ImpVar] public bool is_disabled = false;

    public Action<bool> on_changed;

    public bool is_hovered;
    public bool is_pressed;

    public C2_CheckBox()
    {
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
        layout.size = new Vector2(24, 24);
        layout.size_min = new Vector2(16, 16);
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (style == null) return;

        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        float s = box_size > 0 ? box_size : 16;
        s = MathF.Min(s, MathF.Min(dim.size.X, dim.size.Y));
        float bx = dim.position.X;
        float by = dim.position.Y + (dim.size.Y - s) * 0.5f;

        Color tint = style.tint;
        if (is_disabled) tint = new Color(tint.R, tint.G, tint.B, (byte)120);
        else if (is_pressed) tint = style.tint_pressed;
        else if (is_hovered) tint = style.tint_hovered;

        A_Texture img = is_checked ? style.image_checked : style.image_unchecked;
        if (img != null && img.texture.Id != 0)
        {
            Texture2D tex = img.texture;
            Raylib.DrawTexturePro(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(bx, by, s, s),
                Vector2.Zero, 0f, tint);
        }
        else
        {
            Raylib.DrawRectangleLinesEx(new Rectangle(bx, by, s, s), 1.5f, tint);
            if (is_checked)
                Raylib.DrawRectangleV(new Vector2(bx + 3, by + 3), new Vector2(s - 6, s - 6), tint);
        }

        if (!string.IsNullOrEmpty(text) && Text != null)
        {
            float font_size = Text.size > 0 ? Text.size : 13;
            Font font = Text.font != null ? Text.font.font : Raylib.GetFontDefault();
            Vector2 m = Raylib.MeasureTextEx(font, text, font_size, 1f);
            Vector2 text_pos = new Vector2(bx + s + 6, dim.position.Y + (dim.size.Y - m.Y) * 0.5f);
            Color prev = Text.color;
            if (is_disabled) Text.color = new Color(prev.R, prev.G, prev.B, (byte)120);
            Text.Draw(text, text_pos, new Vector2(m.X + 1, m.Y + 1), 0, ETextWrap.None,
                EUIPositionAlignment.Start, EUIPositionAlignment.Start);
            Text.color = prev;
        }

        is_pressed = false;
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (is_disabled) return;
        if (evnt == ECursorEvent.Select_A)
        {
            is_pressed = true;
            is_checked = !is_checked;
            as_option_select?.Invoke(this);
            on_changed?.Invoke(is_checked);
        }
    }

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin)
        {
            is_hovered = true;
            as_option_hover?.Invoke(this);
        }
        else if (notify == ENotifyGeneric.End)
        {
            is_hovered = false;
            as_option_unhover?.Invoke(this);
        }
    }
}

public class UiStyle_CheckBox : ImpAsset
{
    public static UiStyle_CheckBox DEFAULT = new();

    [ImpVar] public A_Texture image_checked = A_Texture.CHECKBOX_T;
    [ImpVar] public A_Texture image_unchecked = A_Texture.CHECKBOX_F;
    [ImpVar] public Color tint = Color.White;
    [ImpVar] public Color tint_hovered = new Color(220, 235, 255, 255);
    [ImpVar] public Color tint_pressed = new Color(180, 210, 255, 255);
}
