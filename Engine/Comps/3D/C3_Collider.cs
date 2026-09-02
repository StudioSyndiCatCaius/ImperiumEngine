using Engine.Core;

namespace Engine.Comps._3D;

public enum EColliderType { Box, Sphere, Capsule }

public class C3_Collider : Imp3D
{
    public EColliderType type;
}