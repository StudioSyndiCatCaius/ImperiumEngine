using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Comps._2D;

[ImpClass(Common = true)]
public class C2_Image : Imp2D
{
    [ImpVar] public A_Texture texture;
    [ImpVar] public EImageLayout image_layout;
    [ImpVar] public TMargins nine_slice_margins;
    public override bool ChildLayout_IsFree() { return false; }

    public override void OnDraw2D(double dt, EDrawFlags flags = 0)
    {
        base.OnDraw2D(dt, flags);
        if (texture != null) texture.Draw(bounds, new TTransform2(), image_layout, nine_slice_margins);
    }
}