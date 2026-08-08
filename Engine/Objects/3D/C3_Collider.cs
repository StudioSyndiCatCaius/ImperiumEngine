using System.Numerics;
using ImperiumEngine.Classes;
using JoltPhysicsSharp;

namespace ImperiumEngine.Objects._3D;

public enum EColliderShape
{
    Box, Sphere, Capsule, Cylinder, Mesh
}

//a volume collider shape that can block collisions or trigger events on overlap, etc.
public class C3_Collider : ImpPhysic3D
{
    [ImpVar] public EColliderShape shape;
    [ImpVar] public float radius = 0.5f; // half-width, in the XZ plane
    [ImpVar] public float height = 2.0f; // full height along Y

    // Builds the requested primitive, baking in world scale (uniform in XZ — good enough for upright
    // characters and props). Sizes are clamped so degenerate shapes don't trip Jolt's asserts.
    protected override Shape? BuildCollisionShape(Vector3 worldScale)
    {
        float uniform = MathF.Max(worldScale.X, worldScale.Z);
        float r     = MathF.Max(radius * uniform, 0.01f);
        float halfH = MathF.Max(height * 0.5f * worldScale.Y, 0.01f);

        return shape switch
        {
            EColliderShape.Sphere  => new SphereShape(r),
            EColliderShape.Capsule => new CapsuleShape(MathF.Max(halfH - r, 0.01f), r),
            EColliderShape.Box     => new BoxShape(new Vector3(r, halfH, r), 0f),
            _                      => new CylinderShape(halfH, r), // Cylinder (+ Mesh fallback)
        };
    }

    // origin sits at the feet (Unreal-style), so the shape centre is half its height up
    protected override Vector3 ColliderCenterLocal() => new(0, height * 0.5f, 0);
}
