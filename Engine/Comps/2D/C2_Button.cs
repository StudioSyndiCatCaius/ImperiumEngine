using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public enum EButtonLayout
{
    Icon_Text_H, Icon_Text_V, Text_Icon_H, Text_Icon_V,
}

public class C2_Button : ImpComp2D
{
    public string text = "";
    public A_Texture icon = null;
    public EButtonLayout layout = EButtonLayout.Icon_Text_H;
    public UiStyle_Button style = UiStyle_Button.DEFAULT;
    public UiStyle_Text text_style = UiStyle_Text.DEFAULT;

    public float icon_size = 16;
    public int override_font_size = 0;
    public float content_pad = 4;
    public float gap = 4;
    public EUIPositionAlignment content_align_h = EUIPositionAlignment.Center;
    public EUIPositionAlignment content_align_v = EUIPositionAlignment.Center;
    public bool is_disabled = false;

    public Action on_click;

    public bool is_hovered = false;
    public bool is_pressed = false;

    public C2_Button()
    {
        option_button = this;
        cursor_filter = ECursorFilter.Hit;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (style == null) return;

        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        if (is_pressed)
        {
            style.style_pressed.Draw(dim);
            is_pressed = false;
        }
        else if (is_hovered) style.style_hovered.Draw(dim);
        else style.style_unhovered.Draw(dim);

        bool has_icon = icon != null;
        bool has_text = !string.IsNullOrEmpty(text);
        if (!has_icon && !has_text) return;

        float pad = content_pad > 0 ? content_pad : 0;
        Vector2 origin = dim.position + new Vector2(pad, pad);
        Vector2 area = dim.size - new Vector2(pad * 2, pad * 2);
        if (area.X <= 0 || area.Y <= 0) return;

        float iw = 0, ih = 0;
        if (has_icon)
        {
            float s = icon_size > 0 ? icon_size : 16;
            Texture2D tex = icon.texture;
            float tw0 = tex.Width > 0 ? tex.Width : s;
            float th0 = tex.Height > 0 ? tex.Height : s;
            if (tw0 >= th0)
            {
                iw = s;
                ih = s * (th0 / tw0);
            }
            else
            {
                ih = s;
                iw = s * (tw0 / th0);
            }
        }

        float tw = 0, th = 0;
        Font font = text_style?.font != null ? text_style.font.font : Raylib.GetFontDefault();
        float font_size = override_font_size > 0
            ? override_font_size
            : (text_style != null && text_style.size > 0 ? text_style.size : 16);
        if (has_text)
        {
            Vector2 m = Raylib.MeasureTextEx(font, text, font_size, 1f);
            tw = m.X;
            th = m.Y;
        }

        float g = has_icon && has_text ? gap : 0;
        bool vertical = layout is EButtonLayout.Icon_Text_V or EButtonLayout.Text_Icon_V;
        float block_w = vertical ? MathF.Max(iw, tw) : iw + g + tw;
        float block_h = vertical ? ih + g + th : MathF.Max(ih, th);
        float bx = content_align_h switch
        {
            EUIPositionAlignment.Center => origin.X + (area.X - block_w) * 0.5f,
            EUIPositionAlignment.End => origin.X + area.X - block_w,
            _ => origin.X,
        };
        float by = content_align_v switch
        {
            EUIPositionAlignment.Center => origin.Y + (area.Y - block_h) * 0.5f,
            EUIPositionAlignment.End => origin.Y + area.Y - block_h,
            _ => origin.Y,
        };

        Vector2 icon_pos, text_pos;
        switch (layout)
        {
            case EButtonLayout.Text_Icon_H:
                text_pos = new Vector2(bx, by + (block_h - th) * 0.5f);
                icon_pos = new Vector2(bx + tw + g, by + (block_h - ih) * 0.5f);
                break;
            case EButtonLayout.Icon_Text_V:
                icon_pos = new Vector2(bx + (block_w - iw) * 0.5f, by);
                text_pos = new Vector2(bx + (block_w - tw) * 0.5f, by + ih + g);
                break;
            case EButtonLayout.Text_Icon_V:
                text_pos = new Vector2(bx + (block_w - tw) * 0.5f, by);
                icon_pos = new Vector2(bx + (block_w - iw) * 0.5f, by + th + g);
                break;
            default: // Icon_Text_H
                icon_pos = new Vector2(bx, by + (block_h - ih) * 0.5f);
                text_pos = new Vector2(bx + iw + g, by + (block_h - th) * 0.5f);
                break;
        }

        if (has_icon)
        {
            Texture2D tex = icon.texture;
            Raylib.DrawTexturePro(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(icon_pos.X, icon_pos.Y, iw, ih),
                Vector2.Zero, 0f, is_disabled ? new Color(255, 255, 255, 120) : Color.White);
        }

        if (has_text && text_style != null)
        {
            Color prev = text_style.color;
            if (is_disabled) text_style.color = new Color(prev.R, prev.G, prev.B, (byte)120);
            text_style.Draw(text, text_pos, new Vector2(tw + 1, th + 1), override_font_size, ETextWrap.None,
                EUIPositionAlignment.Start, EUIPositionAlignment.Start);
            text_style.color = prev;
        }
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (is_disabled) return;
        if (evnt == ECursorEvent.Select_A)
        {
            is_pressed = true;
            as_option_select?.Invoke(this);
            on_click?.Invoke();
        }
    }

    public override void Cursor_OnEnter(ImpPlayer player)
    {
        base.Cursor_OnEnter(player);
        is_hovered = true;
        as_option_hover?.Invoke(this);
    }

    public override void Cursor_OnExit(ImpPlayer player)
    {
        base.Cursor_OnExit(player);
        is_hovered = false;
        as_option_unhover?.Invoke(this);
    }
}

public class UiStyle_Button : ImpAsset
{
    public static UiStyle_Button DEFAULT = new();

    [ImpVar] public UiStyle_Box style_unhovered = UiStyle_Box.STYLE_BTN_IDLE;
    [ImpVar] public UiStyle_Box style_hovered = UiStyle_Box.STYLE_BTN_HOVER;
    [ImpVar] public UiStyle_Box style_pressed = UiStyle_Box.STYLE_BTN_PRESS;
}
