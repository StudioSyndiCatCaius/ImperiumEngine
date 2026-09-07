using System.Numerics;
using Engine.Assets;
using Engine.Core;

namespace Engine.Comps._3D;

public struct TSplinePoint3D
{
    [ImpVar] public Vector3 position;
    [ImpVar] public Vector3 tangent;
}

public class C3_Spline : Imp3D
{
    [ImpVar] public TSplinePoint3D[] points;
    [ImpVar] public bool is_closed_loop;
    [ImpVar] public A_SplineConfig config;
}
