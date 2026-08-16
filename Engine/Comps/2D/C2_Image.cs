using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Common = true)]
public class C2_Image : Imp2D
{
    [ImpVar] public A_Texture texture;
    [ImpVar] public Color tint = Color.White;

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        //texture.Draw(Dimensions_Get());
        Draw_Texture(Bounds_Get(),Transform_Get(true),texture,tint);
    }
}