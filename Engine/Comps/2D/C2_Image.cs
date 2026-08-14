using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_Image : ImpComp2D
{
    public TImage texture;

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        texture.Draw(Dimensions_Get());
    }
}