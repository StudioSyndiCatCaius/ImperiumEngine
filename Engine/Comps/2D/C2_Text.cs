using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_Text : ImpComp2D
{
    public string text;
    public UIStyle_Text? style;

    // Draws with the theme's dim text instead of its normal text. A flag rather than
    // assigning style_text_dim directly, so the choice still follows a theme swap.
    public bool style_dim;

    // 0 = left, 0.5 = centre, 1 = right. Vertical placement is always centred in the rect.
    public float align = 0f;

    UIStyle_Text Style => style ?? (style_dim ? Theme_Get().style_text_dim : Theme_Get().style_text);

    public C2_Text(string _text)
    {
        this.text = _text;
    }

    // Lets a text comp size itself when no explicit size is set.
    public override Vector2 Size_GetContentMin() => ImpUI.TextMeasure(text, Style);

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        ImpUI.TextInRect(text, rect, Style, align);
    }
}

public class UIStyle_Text : ImpAsset
{
    public A_Font? font;
    public int size=5;
    public float spacing=1f; //extra px between glyphs; a real TTF carries its own advances
    public Color color;

    public int outline_size = 0;
    public Color outline_color=Color.Black;

    public Vector2 shadow_offset;
    public int shadow_size = 0;
    public Color shadow_color=Color.Blank;
}
