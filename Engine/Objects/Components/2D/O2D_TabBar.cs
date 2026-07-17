using System.Drawing;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

public enum ETabBarLayout : byte
{
    Horizontal,
    Vertical,
}

public struct TTabBarOption
{
    public string text;
}

public class O2D_TabBar : ImpComponent2D
{
    public A_UiStyle_TabBar style;
    public ETabBarLayout layout;
    public List<TTabBarOption> options = new();
    public Action<int> OnSelect;

    public int activeIndex = 0;

    private readonly List<O2D_Button> _buttons = new();

    const float TabMinWidth = 90f;

    public void Option_Add(string label)
    {
        options.Add(new TTabBarOption { text = label });

        int index = _buttons.Count;
        var btn = new O2D_Button { text = label };
        btn.custom_minimum_size = new Vector2(TabMinWidth, 0);
        btn.OnPressed = _ => OnSelect?.Invoke(index);
        _buttons.Add(btn);
        Child_Add(btn);
    }

    protected override void Layout_Children(TRect2 rect)
    {
        bool horizontal = layout == ETabBarLayout.Horizontal;
        float pos = horizontal ? rect.position.X : rect.position.Y;

        foreach (var btn in _buttons)
        {
            float w = MathF.Max(btn.Size_GetMinimum().X, TabMinWidth);
            btn.Layout_SetRect(horizontal
                ? new TRect2(new Vector2(pos, rect.position.Y), new Vector2(w, rect.size.Y))
                : new TRect2(new Vector2(rect.position.X, pos), new Vector2(rect.size.X, btn.Size_GetMinimum().Y)));
            pos += horizontal ? w : btn.Size_GetMinimum().Y;
        }
    }

    protected  override void OnDraw(ImpRender rhi, double dt)
    {
        var bgColor = style?.color_bar_background ?? Color.FromArgb(30, 30, 34);
        rhi.Draw2D_Rect(TVector2i.From(_rect.position), TVector2i.From(_rect.size), bgColor);

        for (int i = 0; i < _buttons.Count; i++)
            _buttons[i].style = Buttons_StyleFor(i == activeIndex);

        if (activeIndex >= 0 && activeIndex < _buttons.Count)
        {
            var br = _buttons[activeIndex].Rect_Get();
            var indicatorColor = style?.color_active_indicator ?? Color.FromArgb(100, 150, 255);
            rhi.Draw2D_Rect(
                new TVector2i((int)br.position.X, (int)(br.position.Y + br.size.Y - 3)),
                new TVector2i((int)br.size.X, 3),
                indicatorColor);
        }
    }

    private A_UiStyle_Button Buttons_StyleFor(bool active)
    {
        if (style == null) return active ? A_UiStyle_TabBar.Default_Active : A_UiStyle_TabBar.Default_Inactive;

        return new A_UiStyle_Button
        {
            Style_Normal  = ToButtonState(active ? style.Style_Active        : style.Style_Inactive),
            Style_Hover   = ToButtonState(active ? style.Style_Active_Hover  : style.Style_Inactive_Hover),
            Style_Pressed = ToButtonState(style.Style_Pressed),
        };
    }

    private static TButtonStateStyle ToButtonState(TTabButtonStyle s) => new TButtonStateStyle
    {
        color_background = s.color_background,
        color_content    = s.color_content,
        border_radius    = s.border_radius,
    };
}


// =====================================================================================================================

public struct TTabButtonStyle
{
    public Color color_background;
    public Color color_content;
    public float border_radius;

    public TTabButtonStyle(Color bg, Color content, float radius = 0f)
    {
        color_background = bg;
        color_content    = content;
        border_radius    = radius;
    }
}

public class A_UiStyle_TabBar : ImpAsset
{
    public Color color_bar_background  = Color.FromArgb(30, 30, 34);
    public Color color_active_indicator = Color.FromArgb(100, 150, 255);

    public TTabButtonStyle Style_Inactive      = new(Color.Transparent,          Color.FromArgb(160, 160, 170));
    public TTabButtonStyle Style_Active        = new(Color.Transparent,          Color.White);
    public TTabButtonStyle Style_Inactive_Hover = new(Color.FromArgb(45, 45, 50), Color.FromArgb(200, 200, 210));
    public TTabButtonStyle Style_Active_Hover  = new(Color.FromArgb(45, 45, 50), Color.White);
    public TTabButtonStyle Style_Pressed       = new(Color.FromArgb(20, 20, 24), Color.White);

    // pre-built fallbacks used when no style asset is assigned
    internal static readonly A_UiStyle_Button Default_Inactive = Make(
        Color.Transparent, Color.FromArgb(160, 160, 170),
        Color.FromArgb(45, 45, 50), Color.FromArgb(200, 200, 210),
        Color.FromArgb(20, 20, 24), Color.White);

    internal static readonly A_UiStyle_Button Default_Active = Make(
        Color.Transparent, Color.White,
        Color.FromArgb(45, 45, 50), Color.White,
        Color.FromArgb(20, 20, 24), Color.White);

    private static A_UiStyle_Button Make(
        Color normalBg, Color normalFg,
        Color hoverBg,  Color hoverFg,
        Color pressBg,  Color pressFg) => new A_UiStyle_Button
    {
        Style_Normal  = new TButtonStateStyle { color_background = normalBg, color_content = normalFg },
        Style_Hover   = new TButtonStateStyle { color_background = hoverBg,  color_content = hoverFg  },
        Style_Pressed = new TButtonStateStyle { color_background = pressBg,  color_content = pressFg  },
    };
}
