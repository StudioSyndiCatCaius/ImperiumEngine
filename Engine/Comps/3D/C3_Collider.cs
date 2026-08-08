using System.Numerics;
using ImperiumEngine.Main;

namespace ImperiumEngine.Comps._3D;

public enum ECollisionShape : byte
{
    Cube, Sphere, Cylinder, Capsule, Cone,
}

public class C3_Collider : ImpComp3D
{
    [ImpVar][Export] public ECollisionShape shape;
    [ImpVar][Export] public Vector3 extents;
}