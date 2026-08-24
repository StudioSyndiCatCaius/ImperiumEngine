using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using JoltPhysicsSharp;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Mesh : Imp3D
{
    [ImpVar] public A_Mesh mesh=A_Mesh.GEO_CUBE;
    [ImpVar] public List<A_Material> materials = new();
    [ImpVar] public bool cast_shadows=true;

    public C3_Mesh()
    {
        physics_enabled=true;
        collision_preset=A_CollisionPreset.PRESET_MESH;
    }
    
    public override void OnDraw3D(double dt, EDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (mesh == null || mesh.Submesh_Count() <= 0)
        {
            return;
        }

        TTransform3 t = Transform_Get(true);
        float d = MathF.PI / 180f;
        Quaternion rot = Quaternion.CreateFromYawPitchRoll(t.rotation.Y * d, t.rotation.X * d, t.rotation.Z * d);
        mesh.Draw(t.position, rot, t.scale, materials, cast_shadows);
    }

    protected override TBounds3 Bounds_Calc()
    {
        if (mesh != null && mesh.Submesh_Count() > 0)
        {
            return mesh.Bounds_Get(global_transform);
        }
        return base.Bounds_Calc();
    }

    public override Shape Phys_MakeShape(Vector3 world_scale)
    {
        Vector3 s = new(
            MathF.Abs(world_scale.X),
            MathF.Abs(world_scale.Y),
            MathF.Abs(world_scale.Z));
        if (s.X < 0.01f)
        {
            s.X = 0.01f;
        }
        if (s.Y < 0.01f)
        {
            s.Y = 0.01f;
        }
        if (s.Z < 0.01f)
        {
            s.Z = 0.01f;
        }

        if (mesh != null && ReferenceEquals(mesh, A_Mesh.GEO_PLANE))
        {
            return new BoxShape(new Vector3(s.X * 0.5f, MathF.Max(0.02f, s.Y * 0.02f), s.Z * 0.5f));
        }

        Shape Box()
        {
            return new BoxShape(s * 0.5f);
        }

        Shape Convex_From(List<Vector3> src)
        {
            if (src == null || src.Count < 4)
            {
                return Box();
            }
            if (src.Count > 256)
            {
                return Box();
            }
            Vector3[] points = new Vector3[src.Count];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = src[i];
                points[i] = new Vector3(p.X * s.X, p.Y * s.Y, p.Z * s.Z);
            }
            try
            {
                ConvexHullShapeSettings hull = new(points);
                Shape shape = hull.Create();
                if (shape != null)
                {
                    return shape;
                }
            }
            catch
            {
            }
            return Box();
        }

        if (mesh == null)
        {
            return Box();
        }
        if (mesh.collision_type == EMeshCollision.Box)
        {
            return Box();
        }
        if (mesh.collision_type == EMeshCollision.Triangle && !movement_enabled)
        {
            List<Vector3> src_pts = mesh.collision_points;
            if (src_pts != null && src_pts.Count >= 3)
            {
                Vector3[] verts = new Vector3[src_pts.Count];
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = src_pts[i];
                    verts[i] = new Vector3(p.X * s.X, p.Y * s.Y, p.Z * s.Z);
                }
                List<uint> idx = mesh.collision_indices;
                IndexedTriangle[] tris;
                if (idx != null && idx.Count >= 3)
                {
                    int tri_n = idx.Count / 3;
                    tris = new IndexedTriangle[tri_n];
                    for (int t = 0; t < tri_n; t++)
                    {
                        tris[t] = new IndexedTriangle(idx[t * 3], idx[t * 3 + 1], idx[t * 3 + 2]);
                    }
                }
                else
                {
                    int tri_n = verts.Length / 3;
                    tris = new IndexedTriangle[tri_n];
                    for (int t = 0; t < tri_n; t++)
                    {
                        int b = t * 3;
                        tris[t] = new IndexedTriangle((uint)b, (uint)(b + 1), (uint)(b + 2));
                    }
                }
                if (tris.Length > 0)
                {
                    try
                    {
                        MeshShapeSettings mesh_shape = new(verts, tris);
                        mesh_shape.Sanitize();
                        Shape created = mesh_shape.Create();
                        if (created != null)
                        {
                            return created;
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        List<Vector3> hull_src = mesh.collision_hull;
        if (hull_src == null || hull_src.Count < 4)
        {
            hull_src = mesh.collision_points;
        }
        return Convex_From(hull_src);
    }
}
