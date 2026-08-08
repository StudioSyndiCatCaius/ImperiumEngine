using ImperiumEngine.Assets;
using ImperiumEngine.Main;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// The shared panel primitive: a filled/textured rectangle that other comps sit inside.
public class C2_Rect : ImpComp2D
{
    public UIStyle_Rect? style;

    UIStyle_Rect Style => style ?? Theme_Get().style_rect;

    protected override TMargins Margins_StyleInner() => Style.margins_inner;
    protected override TMargins Margins_StyleOuter() => Style.margins_outer;

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var s = Style;

        if (s.texture_background != null) ImpUI.Texture(s.texture_background, rect, s.color);
        else ImpUI.Rect(rect, s.color);
    }
}

public class UIStyle_Rect : ImpAsset
{

    public TMargins margins_outer; //seperating this rectange from area edges
    public TMargins margins_inner; //seperating this rectange's owning comp children from its inner area edges

    public A_Texture? texture_background;
    public Color color;
}
