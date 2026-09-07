using Engine.Assets;
using Engine.Core;

namespace Engine.Comps._3D;

public enum EColliderType { Box, Sphere, Capsule }

public class C3_Collider : Imp3D
{
    public EColliderType type;

    public C3_Collider()
    {
        type = EColliderType.Box;
        physics_enabled=true;
        collision_preset=A_CollisionPreset.PRESET_MESH;
    }
}