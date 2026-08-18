using System.Numerics;
using ImperiumEngine.Structs;
using JoltPhysicsSharp;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

public enum ECollisionShape : byte
{
    Cube, Sphere, Cylinder, Capsule, Cone,
}

[ImpClass(Common = true)]
public class C3_Collider : Imp3D
{
    [ImpVar] public ECollisionShape shape;
    [ImpVar] public Vector3 extents = Vector3.One;

    static R3D_cs.Mesh _dbg_cube;
    static R3D_cs.Mesh _dbg_sphere;
    static R3D_cs.Mesh _dbg_cylinder;
    static R3D_cs.Mesh _dbg_capsule;
    static R3D_cs.Mesh _dbg_cone;
    static bool _dbg_ready;

    public C3_Collider()
    {
        physics_enabled = true;
    }

    // Local AABB of the collision volume (unscaled). Capsule + movement sits on its feet.
    public void Shape_Local(out Vector3 size, out Vector3 center)
    {
        float x = MathF.Abs(extents.X);
        float y = MathF.Abs(extents.Y);
        float z = MathF.Abs(extents.Z);
        if (x < 0.01f)
        {
            x = 0.01f;
        }
        if (y < 0.01f)
        {
            y = 0.01f;
        }
        if (z < 0.01f)
        {
            z = 0.01f;
        }
        center = Vector3.Zero;
        size = new Vector3(x, y, z);
        if (shape == ECollisionShape.Cube)
        {
            return;
        }
        if (shape == ECollisionShape.Sphere)
        {
            float d = x;
            if (y > d)
            {
                d = y;
            }
            if (z > d)
            {
                d = z;
            }
            size = new Vector3(d, d, d);
            return;
        }
        float rad = x;
        if (z > rad)
        {
            rad = z;
        }
        size = new Vector3(rad, y, rad);
        if (shape == ECollisionShape.Capsule && movement_enabled)
        {
            center = new Vector3(0f, y * 0.5f, 0f);
        }
    }

    public override void OnDraw3D(double dt, EDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (game_owner != null && game_owner != ImpGame.Get(ImpGame.ID_HOST))
        {
            return;
        }
        if (!_dbg_ready)
        {
            _dbg_cube = R3D.GenMeshCube(1f, 1f, 1f);
            _dbg_sphere = R3D.GenMeshSphere(0.5f, 12, 16);
            _dbg_cylinder = R3D.GenMeshCylinder(0.5f, 1f, 16);
            _dbg_capsule = R3D.GenMeshCapsule(0.5f, 1f, 8, 16);
            _dbg_cone = R3D.GenMeshCylinderEx(0.5f, 0f, 1f, 16, 4, true, false);
            _dbg_ready = true;
        }

        R3D_cs.Mesh mesh = _dbg_cube;
        if (shape == ECollisionShape.Sphere)
        {
            mesh = _dbg_sphere;
        }
        else if (shape == ECollisionShape.Cylinder)
        {
            mesh = _dbg_cylinder;
        }
        else if (shape == ECollisionShape.Capsule)
        {
            mesh = _dbg_capsule;
        }
        else if (shape == ECollisionShape.Cone)
        {
            mesh = _dbg_cone;
        }

        Shape_Local(out Vector3 size, out Vector3 center);
        TTransform3 t = Transform_Get(true);
        float d = MathF.PI / 180f;
        Quaternion rot = Quaternion.CreateFromYawPitchRoll(t.rotation.Y * d, t.rotation.X * d, t.rotation.Z * d);
        Vector3 pos = t.position + Vector3.Transform(center * t.scale, rot);
        Vector3 scl = size * t.scale;

        R3D_cs.Material mat = R3D.GetDefaultMaterial();
        AlbedoMap alb = mat.Albedo;
        alb.Color = new Color(40, 210, 255, 70);
        mat.Albedo = alb;
        mat.Unlit = true;
        mat.TransparencyMode = TransparencyMode.Alpha;
        mat.BlendMode = R3D_cs.BlendMode.Mix;
        mat.CullMode = CullMode.None;
        R3D.DrawMeshEx(mesh, mat, pos, rot, scl);
    }

    public override Shape Phys_MakeShape(Vector3 world_scale)
    {
        Vector3 e = new(
            MathF.Abs(extents.X * world_scale.X),
            MathF.Abs(extents.Y * world_scale.Y),
            MathF.Abs(extents.Z * world_scale.Z));
        if (e.X < 0.01f)
        {
            e.X = 0.01f;
        }
        if (e.Y < 0.01f)
        {
            e.Y = 0.01f;
        }
        if (e.Z < 0.01f)
        {
            e.Z = 0.01f;
        }

        if (shape == ECollisionShape.Cube)
        {
            return new BoxShape(e * 0.5f);
        }
        if (shape == ECollisionShape.Sphere)
        {
            float r = e.X;
            if (e.Y > r)
            {
                r = e.Y;
            }
            if (e.Z > r)
            {
                r = e.Z;
            }
            return new SphereShape(r * 0.5f);
        }
        if (shape == ECollisionShape.Cylinder)
        {
            float radius = e.X;
            if (e.Z > radius)
            {
                radius = e.Z;
            }
            return new CylinderShape(e.Y * 0.5f, radius * 0.5f);
        }
        if (shape == ECollisionShape.Capsule)
        {
            float radius = e.X * 0.5f;
            if (e.Z * 0.5f > radius)
            {
                radius = e.Z * 0.5f;
            }
            if (radius < 0.05f)
            {
                radius = 0.05f;
            }
            float height = e.Y;
            float min_h = radius * 2f + 0.05f;
            if (height < min_h)
            {
                height = min_h;
            }
            return new CapsuleShape(height * 0.5f - radius, radius);
        }

        float cone_r = e.X * 0.5f;
        if (e.Z * 0.5f > cone_r)
        {
            cone_r = e.Z * 0.5f;
        }
        float half = e.Y * 0.5f;
        Vector3[] pts = new Vector3[9];
        pts[0] = new Vector3(0f, half, 0f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * (MathF.PI * 2f / 8f);
            pts[i + 1] = new Vector3(MathF.Cos(a) * cone_r, -half, MathF.Sin(a) * cone_r);
        }
        ConvexHullShapeSettings hull = new(pts);
        return hull.Create();
    }

    public override Vector3 Phys_ShapeOffset(Vector3 world_scale)
    {
        if (shape != ECollisionShape.Capsule)
        {
            return Vector3.Zero;
        }
        float height = MathF.Abs(extents.Y * world_scale.Y);
        return new Vector3(0f, height * 0.5f, 0f);
    }
}
