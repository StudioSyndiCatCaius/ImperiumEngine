using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// A tick box with an optional label to its right. The whole rect is clickable, not just
// the box, so it stays usable when stretched across an inspector row.
public class C2_CheckBox : ImpComp2D
{
    [ImpVar] public bool is_checked;
    [ImpVar] public bool is_disabled;
    public string text = "";

    //drawn instead of the tick when the value is indeterminate (multi-select disagreement)
    [ImpVar] public bool is_mixed;

    public UIStyle_Rect? style;
    public UIStyle_Text? style_text;

    public Action<C2_CheckBox>? on_toggled;

    UIStyle_Text StyleText => style_text ?? Theme_Get().style_text;

    float Box_Size() => MathF.Min(Theme_Get().item_height - 8f, rect.Height - 6f);

    public override Vector2 Size_GetContentMin()
    {
        var th = Theme_Get();
        float w = th.item_height + (text.Length > 0 ? ImpUI.TextMeasure(text, StyleText).X + th.padding : 0f);
        return new Vector2(w, th.item_height);
    }

    public void Checked_SetQuiet(bool value)
    {
        is_checked = value;
        is_mixed = false;
    }

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        if (is_disabled || ev != ECursorEvent.Clicked) return;

        // a mixed box resolves to checked on the first click rather than toggling half the targets off
        is_checked = is_mixed || !is_checked;
        is_mixed = false;
        on_toggled?.Invoke(this);
    }

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        float bs = MathF.Max(8f, Box_Size());
        var box = new Rectangle(rect.X + 3f, rect.Y + (rect.Height - bs) * 0.5f, bs, bs);

        var bg = style?.color
                 ?? (is_disabled ? th.col_disabled
                     : ImpUI.IsPressed(this) ? th.col_pressed
                     : ImpUI.IsHovered(this) ? th.col_hover
                     : th.col_panel_alt);

        ImpUI.Rect(box, bg);
        ImpUI.RectOutline(box, 1f, th.col_line);

        if (is_mixed)
        {
            float pad = bs * 0.25f;
            ImpUI.Rect(new Rectangle(box.X + pad, box.Y + bs * 0.5f - 1f, bs - pad * 2f, 2f), th.col_text_dim);
        }
        else if (is_checked)
        {
            float pad = bs * 0.25f;
            ImpUI.Rect(new Rectangle(box.X + pad, box.Y + pad, bs - pad * 2f, bs - pad * 2f),
                       is_disabled ? th.col_text_dim : th.col_accent);
        }

        if (text.Length == 0) return;

        var tr = new Rectangle(box.X + bs + th.padding, rect.Y,
                               MathF.Max(0, rect.Width - bs - th.padding), rect.Height);
        ImpUI.TextInRect(text, tr, StyleText, 0f);
    }
}
