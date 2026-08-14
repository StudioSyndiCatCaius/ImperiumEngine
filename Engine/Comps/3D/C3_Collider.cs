using System.Numerics;

namespace ImperiumEngine.Comps._3D;

public enum ECollisionShape : byte
{
    Cube, Sphere, Cylinder, Capsule, Cone,
}

public class C3_Collider : ImpComp3D
{
    [ImpVar]public ECollisionShape shape;
    [ImpVar] public Vector3 extents;
}