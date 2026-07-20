using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects._3D;

public enum EColliderShape
{
    Box, Sphere, Capsule, Mesh
}

//a volume collider shape that can block collisions or trigger events on overlap, etc.
public class C3_Collider : ImpComponent3D
{
    public EColliderShape shape;
}