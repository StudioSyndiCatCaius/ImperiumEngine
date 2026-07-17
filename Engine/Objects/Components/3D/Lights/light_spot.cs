using ImperiumCore;

namespace ImperiumEngine.Objects._3D.Lights;

[Title( "Light : Spot")]
public abstract class O3D_Light_Spot : O3D_Light
{
    [ImpVar][Exposed] public float ConeAngle;
    [ImpVar][Exposed] public float Radius;
}