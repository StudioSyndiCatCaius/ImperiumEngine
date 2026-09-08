using System.Numerics;
using Engine.Assets;
using Engine.Core;
using Engine.Structs;
using JoltPhysicsSharp;

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

    public override TBounds3 Bounds_Cache()
    {
        Vector3 s = global_transform.scale;
        float ax = MathF.Abs(s.X);
        float ay = MathF.Abs(s.Y);
        float az = MathF.Abs(s.Z);
        Vector3 size = type switch
        {
            EColliderType.Sphere => Vector3.One * MathF.Max(ax, MathF.Max(ay, az)),
            EColliderType.Capsule => new Vector3(
                ImpPhysics.CapsuleRadius(this) * 2f,
                ImpPhysics.CapsuleHeight(this),
                ImpPhysics.CapsuleRadius(this) * 2f),
            _ => new Vector3(ax, ay, az),
        };
        return new TBounds3
        {
            center = global_transform.position,
            size = size,
            rotation = global_transform.rotation,
        };
    }

    public override Shape? Phys_GetShape()
    {
        Vector3 s = global_transform.scale;
        float ax = MathF.Max(MathF.Abs(s.X), 0.05f);
        float ay = MathF.Max(MathF.Abs(s.Y), 0.05f);
        float az = MathF.Max(MathF.Abs(s.Z), 0.05f);
        switch (type)
        {
            case EColliderType.Sphere:
            {
                float r = 0.5f * MathF.Max(ax, MathF.Max(ay, az));
                return new SphereShape(r);
            }
            case EColliderType.Capsule:
            {
                float radius = ImpPhysics.CapsuleRadius(this);
                if (radius < 0.05f) radius = 0.05f;
                float height = ImpPhysics.CapsuleHeight(this);
                float cyl = height - radius * 2f;
                if (cyl < 0.02f) cyl = 0.02f;
                float half = cyl * 0.5f;
                CapsuleShape capsule = new(half, radius);
                if (movement_enabled)
                    return new RotatedTranslatedShape(new Vector3(0f, half + radius, 0f), Quaternion.Identity, capsule);
                return capsule;
            }
            default:
                return ImpPhysics.MakeBox(new Vector3(ax, ay, az) * 0.5f);
        }
    }
}