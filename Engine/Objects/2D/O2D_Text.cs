using System.Drawing;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Enums;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

public class O2D_Text : ImpComponent2D
{
    [ImpVar] [Exposed] public string text;
    [ImpVar] [Exposed] public bool wrap=true;
    [ImpVar] [Exposed] public bool isRichText;
    
    [ImpVar] [Exposed] public A_UiStyle_Text style = new A_UiStyle_Text();

    
    
    public O2D_Text() => mouse_filter = EMouseFilter.Ignore;

    public override void OnDraw(ImpRender rhi, double dt)
    {
        if (string.IsNullOrEmpty(text)) return;
        rhi.Draw2D_Text(TVector2i.From(_rect.position), text, style.font_size, style.color);
    }

    public override Vector2 Size_GetMinimum()
    {
        var own = base.Size_GetMinimum();
        if (string.IsNullOrEmpty(text)) return own;

        var m = ImpRender.Get().Text_Measure(text, style.font_size);
        return Vector2.Max(new Vector2(m.x, m.y), own);
    }
}


public class A_UiStyle_Text : ImpAsset
{
    public Color color = Color.White;
    public int font_size = 16;
    
    public bool use_outline;
    public Color outline_Color;
    public float outline_thickness;
    
    public bool use_shadow;
    public TVector2i shadow_offset;
    public float shadow_sharpness;
}