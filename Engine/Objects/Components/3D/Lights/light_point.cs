using ImperiumCore;

namespace ImperiumEngine.Objects._3D.Lights;

[Title( "Light : Point")]
public abstract class O3D_Light_Point : O3D_Light
{
    [ImpVar][Exposed] public float Radius;
}
