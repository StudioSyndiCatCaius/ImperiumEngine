using System.Drawing;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;
using ImperiumEngine.Assets;



namespace ImperiumEngine.Objects._2D;

public enum EButtonIconPosition
{
    Left,
    Right,
    Top,
    Bottom,
}

public class O2D_Button : ImpComponent2D
{
    public string text;
    public A_UiStyle_Button style = new A_UiStyle_Button();

    [ImpVar][Exposed] public int font_size = 16;

    public A_Texture2D icon;
    public Vector2 icon_size=new Vector2(16, 16);
    public EButtonIconPosition icon_position = EButtonIconPosition.Left;
    public float icon_separation = 6f;
    
    public Action<O2D_Button> OnPressed;
    public Action<O2D_Button> OnReleased;

    // fallback colors used when no style asset is assigned
    [ImpVar][Exposed] public Color color = Color.FromArgb(70, 70, 76);
    [ImpVar][Exposed] public Color color_hover = Color.FromArgb(92, 92, 100);
    [ImpVar][Exposed] public Color color_pressed = Color.FromArgb(50, 50, 56);

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (style != null) { Draw_Styled(rhi, State_Style()); return; }

        var c = pressed ? color_pressed : hovered ? color_hover : color;
        rhi.Draw2D_Button(TVector2i.From(_rect.position), TVector2i.From(_rect.size), text, c);
    }

    public override Vector2 Size_GetMinimum()
    {
        var own = base.Size_GetMinimum();

        Vector2 textSize = Vector2.Zero;
        if (!string.IsNullOrEmpty(text))
        {
            var m = ImpRender.Get().Text_Measure(text, font_size);
            textSize = new Vector2(m.x, m.y);
        }

        Vector2 contentSize = textSize;
        bool hasIcon = icon != null;
        bool hasText = !string.IsNullOrEmpty(text);

        if (hasIcon)
        {
            if (hasText)
            {
                bool horizontal = icon_position is EButtonIconPosition.Left or EButtonIconPosition.Right;
                contentSize = horizontal
                    ? new Vector2(icon_size.X + icon_separation + textSize.X, MathF.Max(icon_size.Y, textSize.Y))
                    : new Vector2(MathF.Max(icon_size.X, textSize.X), icon_size.Y + icon_separation + textSize.Y);
            }
            else
            {
                contentSize = icon_size;
            }
        }

        if (style != null) contentSize += Inset_Of(style.Style_Normal);
        return Vector2.Max(contentSize, own);
    }

    public override void OnClicked() => OnPressed?.Invoke(this);
    public override void OnMouseReleased(int button) => OnReleased?.Invoke(this);

    // ----------------------------------------------------------------------------------------------------------------

    private TButtonStateStyle State_Style() =>
        pressed ? style.Style_Pressed : hovered ? style.Style_Hover : style.Style_Normal;

    private void Draw_Styled(ImpRender rhi, TButtonStateStyle s)
    {
        var pos = _rect.position;
        var size = _rect.size;

        float bl = (float)s.border.left, bt = (float)s.border.top;
        float br = (float)s.border.right, bb = (float)s.border.bottom;
        bool hasBorder = bl > 0 || bt > 0 || br > 0 || bb > 0;

        // border frame drawn behind the background (no dedicated border color → use content color)
        if (hasBorder)
            rhi.Draw2D_RectRounded(TVector2i.From(pos), TVector2i.From(size), s.color_content, s.border_radius);

        var inPos = new Vector2(pos.X + bl, pos.Y + bt);
        var inSize = new Vector2(size.X - bl - br, size.Y - bt - bb);
        float inRadius = MathF.Max(0f, s.border_radius - MathF.Max(MathF.Max(bl, bt), MathF.Max(br, bb)));
        rhi.Draw2D_RectRounded(TVector2i.From(inPos), TVector2i.From(inSize), s.color_background, inRadius);

        float pl = (float)s.padding.left, pt = (float)s.padding.top;
        float pr = (float)s.padding.right, pb = (float)s.padding.bottom;
        var contentPos = new Vector2(inPos.X + pl, inPos.Y + pt);
        var contentSize = new Vector2(inSize.X - pl - pr, inSize.Y - pt - pb);

        Draw_Content(rhi, s, contentPos, contentSize);
    }

    private void Draw_Content(ImpRender rhi, TButtonStateStyle s, Vector2 contentPos, Vector2 contentSize)
    {
        bool hasIcon = icon != null;
        bool hasText = !string.IsNullOrEmpty(text);

        if (!hasIcon && !hasText) return;

        Vector2 textSize = Vector2.Zero;
        if (hasText)
        {
            var m = rhi.Text_Measure(text, font_size);
            textSize = new Vector2(m.x, m.y);
        }
        Vector2 iconSize = hasIcon ? icon_size : Vector2.Zero;

        // icon-only / text-only: just center the single element
        if (!hasIcon || !hasText)
        {
            var single = hasIcon ? iconSize : textSize;
            var at = contentPos + (contentSize - single) * 0.5f;
            if (hasIcon) rhi.Draw2D_Texture(icon, TVector2i.From(at), TVector2i.From(iconSize), s.color_content);
            else rhi.Draw2D_Text(TVector2i.From(at), text, font_size, s.color_content);
            return;
        }

        // icon + text: size the group, center it, then place the two parts along the axis
        bool horizontal = icon_position is EButtonIconPosition.Left or EButtonIconPosition.Right;
        var group = horizontal
            ? new Vector2(iconSize.X + icon_separation + textSize.X, MathF.Max(iconSize.Y, textSize.Y))
            : new Vector2(MathF.Max(iconSize.X, textSize.X), iconSize.Y + icon_separation + textSize.Y);

        var origin = contentPos + (contentSize - group) * 0.5f;

        Vector2 iconPos, textPos;
        switch (icon_position)
        {
            case EButtonIconPosition.Left:
                iconPos = new Vector2(origin.X, origin.Y + (group.Y - iconSize.Y) * 0.5f);
                textPos = new Vector2(origin.X + iconSize.X + icon_separation, origin.Y + (group.Y - textSize.Y) * 0.5f);
                break;
            case EButtonIconPosition.Right:
                textPos = new Vector2(origin.X, origin.Y + (group.Y - textSize.Y) * 0.5f);
                iconPos = new Vector2(origin.X + textSize.X + icon_separation, origin.Y + (group.Y - iconSize.Y) * 0.5f);
                break;
            case EButtonIconPosition.Top:
                iconPos = new Vector2(origin.X + (group.X - iconSize.X) * 0.5f, origin.Y);
                textPos = new Vector2(origin.X + (group.X - textSize.X) * 0.5f, origin.Y + iconSize.Y + icon_separation);
                break;
            default: // Bottom
                textPos = new Vector2(origin.X + (group.X - textSize.X) * 0.5f, origin.Y);
                iconPos = new Vector2(origin.X + (group.X - iconSize.X) * 0.5f, origin.Y + textSize.Y + icon_separation);
                break;
        }

        rhi.Draw2D_Texture(icon, TVector2i.From(iconPos), TVector2i.From(iconSize), s.color_content);
        rhi.Draw2D_Text(TVector2i.From(textPos), text, font_size, s.color_content);
    }

    private static Vector2 Inset_Of(TButtonStateStyle s) => new(
        (float)(s.border.left + s.border.right + s.padding.left + s.padding.right),
        (float)(s.border.top + s.border.bottom + s.padding.top + s.padding.bottom));
}

public struct TButtonStateStyle
{
    public Color color_background=Color.Gray;
    public Color color_content=Color.White;
    public TMargin2D border;
    public TMargin2D padding;
    public float border_radius=7.0f;
    

    public TButtonStateStyle()
    {
        border = default;
        padding = default;
    }
}

public class A_UiStyle_Button : ImpAsset
{
    public TButtonStateStyle Style_Normal, Style_Hover, Style_Pressed;
    
    const float default_borderRadius = 10f;
    
    
    public A_UiStyle_Button()
    {
        Style_Normal = new TButtonStateStyle
        {
            color_background = Color.FromArgb(70, 70, 76),
        };

        Style_Hover = new TButtonStateStyle
        {
            color_background = Color.FromArgb(92, 92, 100),
        };

        Style_Pressed = new TButtonStateStyle
        {
            color_background = Color.FromArgb(50, 50, 56),
        };
    }
}