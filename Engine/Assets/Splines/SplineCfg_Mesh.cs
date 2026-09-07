using System.Numerics;
using Engine.Comps._3D;

namespace Engine.Assets.Splines;

//drawing meshes along a spline (For now, just a cube)
public class SplineCfg_Mesh : A_SplineConfig
{
    [ImpVar] public Vector3 scale=Vector3.One;
    [ImpVar] public bool bend_with_spline;
}