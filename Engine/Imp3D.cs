using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine;

public struct TTraceResult3D
{
    public bool hit;
    public ImpComp3D? hit_comp;
    public Vector3 hit_position;
    public Vector3 hit_normal;
    public Vector3 start_position;
    public Vector3 start_normal;
    
}

// Drawing 3D
public static class Imp3D
{
    // #################################################################################
    // Static
    // #################################################################################
    
    public static void Draw_Line(Vector3 start, Vector3 end, float thickness, Color color)
    {
        Raylib.DrawLine3D(start, end, color);
    }
    
    // ---------------------------------
    // Trace
    // ---------------------------------
    public static TTraceResult3D Trace_Line(Vector3 start, Vector3 end, ECollisionChannel channel,
        Func<ImpComp3D, bool> filter = null)
    {
        return default;
    }

    public static bool Ray_Plane(Ray ray, Vector3 plane_p, Vector3 plane_n, out Vector3 hit)
    {
        hit = default;
        float denom = Vector3.Dot(ray.Direction, plane_n);
        if (MathF.Abs(denom) < 1e-7f) return false;
        float t = Vector3.Dot(plane_p - ray.Position, plane_n) / denom;
        if (t < 0f) return false;
        hit = ray.Position + ray.Direction * t;
        return true;
    }

    public static bool Ray_AABB(Ray ray, Vector3 min, Vector3 max, out float t)
    {
        t = 0f;
        Vector3 inv = new(
            MathF.Abs(ray.Direction.X) > 1e-12f ? 1f / ray.Direction.X : 1e12f,
            MathF.Abs(ray.Direction.Y) > 1e-12f ? 1f / ray.Direction.Y : 1e12f,
            MathF.Abs(ray.Direction.Z) > 1e-12f ? 1f / ray.Direction.Z : 1e12f);
        float t1 = (min.X - ray.Position.X) * inv.X;
        float t2 = (max.X - ray.Position.X) * inv.X;
        float t3 = (min.Y - ray.Position.Y) * inv.Y;
        float t4 = (max.Y - ray.Position.Y) * inv.Y;
        float t5 = (min.Z - ray.Position.Z) * inv.Z;
        float t6 = (max.Z - ray.Position.Z) * inv.Z;
        float tmin = MathF.Max(MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4)), MathF.Min(t5, t6));
        float tmax = MathF.Min(MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4)), MathF.Max(t5, t6));
        if (tmax < 0f || tmin > tmax) return false;
        t = tmin >= 0f ? tmin : tmax;
        return t >= 0f;
    }

    public static bool Pickable(ImpComp3D c) => c != null && !c.IsGroupPivot;

    public static void Comp3D_LocalBounds(ImpComp3D c, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(-0.2f);
        max = new Vector3(0.2f);
        if (c is not C3_Mesh mesh || mesh.mesh == null) return;
        if (ReferenceEquals(mesh.mesh, A_Mesh.GEO_PLANE))
        {
            min = new Vector3(-0.5f, -0.02f, -0.5f);
            max = new Vector3(0.5f, 0.02f, 0.5f);
            return;
        }
        min = new Vector3(-0.5f);
        max = new Vector3(0.5f);
    }

    public static bool Ray_Comp3D(Ray ray, ImpComp3D c, out float t, out Vector3 hit)
    {
        t = 0f;
        hit = default;
        if (c == null || !c.is_visible) return false;
        TTransform3 w = c.Transform_Get(true);
        Quaternion q = ImpMath.EulerToQuat(w.rotation);
        Quaternion inv_q = Quaternion.Inverse(q);
        Vector3 inv_s = new(
            w.scale.X != 0 ? 1f / w.scale.X : 0,
            w.scale.Y != 0 ? 1f / w.scale.Y : 0,
            w.scale.Z != 0 ? 1f / w.scale.Z : 0);
        Vector3 o = Vector3.Transform(ray.Position - w.position, inv_q) * inv_s;
        Vector3 d = Vector3.Transform(ray.Direction, inv_q) * inv_s;
        Comp3D_LocalBounds(c, out Vector3 min, out Vector3 max);
        Ray local = new(o, d);
        if (!Ray_AABB(local, min, max, out t)) return false;
        hit = ray.Position + ray.Direction * t;
        return true;
    }

    public static ImpComp3D Pick_Comp3D(ImpComp root, Ray ray, out Vector3 hit)
    {
        ImpComp3D best = null;
        Vector3 best_hit = default;
        float best_t = float.MaxValue;
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible) return;
            if (n is ImpComp3D c3 && n is not C3_Gizmo && Imp3D.Pickable(c3))
            {
                if (Ray_Comp3D(ray, c3, out float t, out Vector3 h) && t < best_t)
                {
                    best_t = t;
                    best = c3;
                    best_hit = h;
                }
            }
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        Walk(root);
        hit = best_hit;
        return best;
    }

    static readonly List<ImpComp3D> _occ = new();

    public static void Occ_Gather(ImpComp root)
    {
        _occ.Clear();
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible) return;
            if (n is ImpComp3D c3 && n is not C3_Gizmo && Imp3D.Pickable(c3)) _occ.Add(c3);
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        Walk(root);
    }

    public static bool Occ_Point(Camera3D cam, Vector3 world, float bias = 0.03f)
    {
        Vector3 dir = world - cam.Position;
        float dist = dir.Length();
        if (dist < 1e-4f) return false;
        Ray ray = new(cam.Position, dir / dist);
        for (int i = 0; i < _occ.Count; i++)
        {
            if (!Ray_Comp3D(ray, _occ[i], out float t, out _)) continue;
            if (t > 0f && t < dist - bias) return true;
        }
        return false;
    }

    public static void Comp3D_WorldCorners(ImpComp3D c, Vector3[] corners)
    {
        TTransform3 w = c.Transform_Get(true);
        Quaternion q = ImpMath.EulerToQuat(w.rotation);
        Comp3D_LocalBounds(c, out Vector3 min, out Vector3 max);
        Vector3[] local =
        {
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z),
            new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z),
            new(min.X, max.Y, max.Z), new(max.X, max.Y, max.Z),
        };
        for (int i = 0; i < 8; i++)
            corners[i] = w.position + Vector3.Transform(local[i] * w.scale, q);
    }
}