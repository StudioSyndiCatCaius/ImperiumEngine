using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public enum EButtonLayout
{
    Icon_Text_H, Text_Icon_H, Icon_Text_V, Text_Icon_V,
}

public class C2_Button : ImpComp2D
{
    [ImpVar] public string text = "";
    [ImpVar] public A_Texture? icon;
    [ImpVar] public UIStyle_Button? style;
    [ImpVar] public UIStyle_Text? style_text;
    [ImpVar] public EButtonLayout layout = EButtonLayout.Icon_Text_H;
    
    [ImpVar] public bool is_disabled = false;

    public Action<C2_Button>? on_click;

    // Colour actually drawn, eased towards the target state when blending is enabled.
    Color col_current;
    bool col_primed = false;

    UIStyle_Button Style => style ?? Theme_Get().style_button;
    UIStyle_Text StyleText => style_text ?? Theme_Get().style_text;

    protected override TMargins Margins_StyleInner() => State_GetRect().margins_inner;
    protected override TMargins Margins_StyleOuter() => State_GetRect().margins_outer;

    public override Vector2 Size_GetContentMin()
    {
        var m = ImpUI.TextMeasure(text, StyleText);
        float pad = Theme_Get().padding;
        return new Vector2(m.X + pad * 2, m.Y + pad);
    }

    // Picks the style rect matching the button's current interaction state.
    UIStyle_Rect State_GetRect()
    {
        var s = Style;
        var th = Theme_Get();

        if (is_disabled) return s.rect_disabled ?? th.style_button_disabled;
        if (ImpUI.IsPressed(this)) return s.rect_pressed ?? th.style_button_pressed;
        if (ImpUI.IsHovered(this)) return s.rect_hovered ?? th.style_button_hovered;
        return s.rect_normal ?? th.style_button_normal;
    }

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        if (is_disabled) return;
        if (ev == ECursorEvent.Clicked) on_click?.Invoke(this);
    }

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var s = Style;
        var target = State_GetRect();

        // ease between states so hover/press transitions aren't a hard snap
        if (!col_primed)
        {
            col_current = target.color;
            col_primed = true;
        }
        else if (s.use_blend_time && s.blend_time > 0f)
        {
            col_current = ImpUI.Color_Lerp(col_current, target.color, (float)(dt / s.blend_time));
        }
        else
        {
            col_current = target.color;
        }

        if (target.texture_background != null) ImpUI.Texture(target.texture_background, rect, col_current);
        else ImpUI.Rect(rect, col_current);

        ImpUI.TextInRect(text, rect, StyleText, 0.5f);
    }
}

public class UIStyle_Button : ImpAsset
{
    public UIStyle_Rect? rect_normal;
    public UIStyle_Rect? rect_hovered;
    public UIStyle_Rect? rect_disabled;
    public UIStyle_Rect? rect_pressed;
    public bool use_blend_time=false;
    public float blend_time=0.1f;
}
