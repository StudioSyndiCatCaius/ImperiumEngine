using System.Numerics;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;
using R3D_cs;
using Raylib_cs;

namespace Engine.Globals;

public static class G3D
{
    private static R3D_cs.Mesh _line_mesh;
    private static bool _line_mesh_ready;

    public static TBounds3 Draw3D_Line(Vector3 start, Vector3 end, float thickness, Color color)
    {
        Vector3 delta = end - start;
        float len = delta.Length();
        if (len < 1e-6f)
        {
            return TBounds3.ZERO;
        }
        if (thickness < 0.001f)
        {
            thickness = 0.001f;
        }
        if (!_line_mesh_ready)
        {
            _line_mesh = R3D.GenMeshCylinder(0.5f, 1f, 8);
            _line_mesh.ShadowCastMode = ShadowCastMode.Disabled;
            _line_mesh_ready = true;
        }

        Vector3 dir = delta / len;
        Vector3 mid = (start + end) * 0.5f;
        Quaternion rot;
        float along = Vector3.Dot(Vector3.UnitY, dir);
        if (along > 0.9999f)
        {
            rot = Quaternion.Identity;
        }
        else if (along < -0.9999f)
        {
            rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
        }
        else
        {
            Vector3 axis = Vector3.Cross(Vector3.UnitY, dir);
            rot = Quaternion.Normalize(new Quaternion(axis.X, axis.Y, axis.Z, 1f + along));
        }

        R3D_cs.Material mat = R3D.GetDefaultMaterial();
        AlbedoMap alb = mat.Albedo;
        alb.Color = color;
        mat.Albedo = alb;
        mat.Unlit = true;
        R3D.DrawMeshEx(_line_mesh, mat, mid, rot, new Vector3(thickness, len, thickness));
        return new TBounds3
        {
            center = mid,
            size = new Vector3(thickness, len, thickness),
            rotation = GMath.Quat_2_Euler(rot),
        };
    }

    public static TBounds3 Draw3D_Box(TTransform3 transform, Vector3 bounds, float thickness =0.4f, Color color = default)
    {
        if (color.A == 0)
        {
            color = Color.White;
        }
        Quaternion rot = GMath.Euler_2_Quat(transform.rotation);
        Vector3 h = new Vector3(
            MathF.Abs(bounds.X * transform.scale.X) * 0.5f,
            MathF.Abs(bounds.Y * transform.scale.Y) * 0.5f,
            MathF.Abs(bounds.Z * transform.scale.Z) * 0.5f);
        Vector3 c = transform.position;
        Vector3 P(float x, float y, float z)
        {
            return c + Vector3.Transform(new Vector3(x, y, z), rot);
        }
        Vector3 p000 = P(-h.X, -h.Y, -h.Z);
        Vector3 p001 = P(-h.X, -h.Y,  h.Z);
        Vector3 p010 = P(-h.X,  h.Y, -h.Z);
        Vector3 p011 = P(-h.X,  h.Y,  h.Z);
        Vector3 p100 = P( h.X, -h.Y, -h.Z);
        Vector3 p101 = P( h.X, -h.Y,  h.Z);
        Vector3 p110 = P( h.X,  h.Y, -h.Z);
        Vector3 p111 = P( h.X,  h.Y,  h.Z);
        Draw3D_Line(p000, p001, thickness, color);
        Draw3D_Line(p001, p101, thickness, color);
        Draw3D_Line(p101, p100, thickness, color);
        Draw3D_Line(p100, p000, thickness, color);
        Draw3D_Line(p010, p011, thickness, color);
        Draw3D_Line(p011, p111, thickness, color);
        Draw3D_Line(p111, p110, thickness, color);
        Draw3D_Line(p110, p010, thickness, color);
        Draw3D_Line(p000, p010, thickness, color);
        Draw3D_Line(p001, p011, thickness, color);
        Draw3D_Line(p101, p111, thickness, color);
        Draw3D_Line(p100, p110, thickness, color);
        return new TBounds3
        {
            center = c,
            size = h * 2f,
            rotation = transform.rotation,
        };
    }

    public static TBounds3 Draw3D_Capsule(TTransform3 transform, float radius, float height, int slices = 16, Color color = default)
    {
        if (radius < 0.001f || height < 0.001f)
        {
            return TBounds3.ZERO;
        }
        if (color.A == 0)
        {
            color = Color.White;
        }
        if (slices < 4)
        {
            slices = 4;
        }
        Quaternion q = GMath.Euler_2_Quat(transform.rotation);
        float rx = MathF.Abs(transform.scale.X) * radius;
        float ry = MathF.Abs(transform.scale.Y) * radius;
        float rz = MathF.Abs(transform.scale.Z) * radius;
        float hy = MathF.Abs(transform.scale.Y) * height;
        if (hy < ry * 2f)
        {
            hy = ry * 2f;
        }
        float thick = Math.Clamp(MathF.Min(rx, rz) * 0.03f, 0.01f, 0.08f);
        Vector3 o = transform.position;
        Vector3 top_c = new Vector3(0f, hy * 0.5f - ry, 0f);
        Vector3 bot_c = new Vector3(0f, -hy * 0.5f + ry, 0f);
        DrawWireCircle(o, q, top_c, new Vector3(rx, 0f, 0f), new Vector3(0f, 0f, rz), slices, thick, color);
        DrawWireCircle(o, q, bot_c, new Vector3(rx, 0f, 0f), new Vector3(0f, 0f, rz), slices, thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(rx, 0f, 0f), top_c + new Vector3(rx, 0f, 0f), thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(-rx, 0f, 0f), top_c + new Vector3(-rx, 0f, 0f), thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(0f, 0f, rz), top_c + new Vector3(0f, 0f, rz), thick, color);
        DrawWireLine(o, q, bot_c + new Vector3(0f, 0f, -rz), top_c + new Vector3(0f, 0f, -rz), thick, color);
        int steps = slices / 2;
        if (steps < 4)
        {
            steps = 4;
        }

        DrawWireHemi(o, q, top_c, rx, ry, rz, 1f, steps, thick, color);
        DrawWireHemi(o, q, bot_c, rx, ry, rz, -1f, steps, thick, color);
        return new TBounds3
        {
            center = o,
            size = new Vector3(rx * 2f, hy, rz * 2f),
            rotation = transform.rotation,
        };
    }

    public static TBounds3 Draw3D_Sphere(TTransform3 transform, float radius, int slices = 16, Color color = default)
    {
        if (radius < 0.001f) return TBounds3.ZERO;
        if (color.A == 0) color = Color.White;
        if (slices < 4) slices = 4;
        Quaternion q = GMath.Euler_2_Quat(transform.rotation);
        Vector3 r = new Vector3(
            MathF.Abs(transform.scale.X) * radius,
            MathF.Abs(transform.scale.Y) * radius,
            MathF.Abs(transform.scale.Z) * radius);
        float thick = Math.Clamp(MathF.Min(r.X, MathF.Min(r.Y, r.Z)) * 0.03f, 0.01f, 0.08f);
        Vector3 o = transform.position;
        DrawWireCircle(o, q, Vector3.Zero, new Vector3(r.X, 0f, 0f), new Vector3(0f, r.Y, 0f), slices, thick, color);
        DrawWireCircle(o, q, Vector3.Zero, new Vector3(r.X, 0f, 0f), new Vector3(0f, 0f, r.Z), slices, thick, color);
        DrawWireCircle(o, q, Vector3.Zero, new Vector3(0f, r.Y, 0f), new Vector3(0f, 0f, r.Z), slices, thick, color);
        return new TBounds3
        {
            center = o,
            size = r * 2f,
            rotation = transform.rotation,
        };
    }

    public static TBounds3 Draw3D_Arrow(TTransform3 transform, float length, float thickness=0.3f, Color color=default)
    {
        if (length < 0.001f)
        {
            return TBounds3.ZERO;
        }
        if (color.A == 0)
        {
            color = Color.White;
        }
        if (thickness < 0.001f)
        {
            thickness = 0.001f;
        }
        Quaternion q = GMath.Euler_2_Quat(transform.rotation);
        float len = length * MathF.Abs(transform.scale.Z);
        if (len < 0.001f)
        {
            return TBounds3.ZERO;
        }
        Vector3 o = transform.position;
        Vector3 tip = new Vector3(0f, 0f, -len);
        float head = len * 0.22f;
        float head_w = len * 0.1f;
        Vector3 hb = new Vector3(0f, 0f, -len + head);
        DrawWireLine(o, q, Vector3.Zero, tip, thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(head_w, 0f, 0f), thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(-head_w, 0f, 0f), thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(0f, head_w, 0f), thickness, color);
        DrawWireLine(o, q, tip, hb + new Vector3(0f, -head_w, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(head_w, 0f, 0f), hb + new Vector3(0f, head_w, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(0f, head_w, 0f), hb + new Vector3(-head_w, 0f, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(-head_w, 0f, 0f), hb + new Vector3(0f, -head_w, 0f), thickness, color);
        DrawWireLine(o, q, hb + new Vector3(0f, -head_w, 0f), hb + new Vector3(head_w, 0f, 0f), thickness, color);
        return new TBounds3
        {
            center = o + Vector3.Transform(new Vector3(0f, 0f, -len * 0.5f), q),
            size = new Vector3(head_w * 2f, head_w * 2f, len),
            rotation = transform.rotation,
        };
    }

    private static void DrawWireLine(Vector3 origin, Quaternion rot, Vector3 a, Vector3 b, float thickness, Color color)
    {
        Draw3D_Line(origin + Vector3.Transform(a, rot), origin + Vector3.Transform(b, rot), thickness, color);
    }

    private static void DrawWireCircle(Vector3 origin, Quaternion rot, Vector3 center, Vector3 axis_a, Vector3 axis_b, int slices, float thickness, Color color)
    {
        Vector3 prev = default;
        for (int i = 0; i <= slices; i++)
        {
            float t = (float)i / slices * MathF.PI * 2f;
            Vector3 lp = center + axis_a * MathF.Cos(t) + axis_b * MathF.Sin(t);
            Vector3 wp = origin + Vector3.Transform(lp, rot);
            if (i > 0)
            {
                Draw3D_Line(prev, wp, thickness, color);
            }
            prev = wp;
        }
    }

    private static void DrawWireHemi(Vector3 origin, Quaternion rot, Vector3 center, float rx, float ry, float rz, float y_sign, int steps, float thickness, Color color)
    {
        Vector3[] rad =
        {
            new Vector3(rx, 0f, 0f),
            new Vector3(-rx, 0f, 0f),
            new Vector3(0f, 0f, rz),
            new Vector3(0f, 0f, -rz),
        };
        for (int m = 0; m < 4; m++)
        {
            Vector3 prev = default;
            for (int i = 0; i <= steps; i++)
            {
                float a = (float)i / steps * MathF.PI * 0.5f;
                Vector3 lp = center + rad[m] * MathF.Cos(a) + new Vector3(0f, y_sign * ry * MathF.Sin(a), 0f);
                Vector3 wp = origin + Vector3.Transform(lp, rot);
                if (i > 0)
                {
                    Draw3D_Line(prev, wp, thickness, color);
                }
                prev = wp;
            }
        }
    }

    public static TBounds3 Draw3D_Billboard(A_Texture billboard, TTransform3 transform, float size=1.0f, Color color = default)
    {
        Raylib.DrawBillboard(App.view_target_data,billboard.texture, transform.position, size, color);
        return default;
    }
    
    // ---------------------------------------------------------------------------------------------------
    // Trace
    // ---------------------------------------------------------------------------------------------------

    public static TTraceResult3D Trace3D_Line(Vector3 start, Vector3 end, ECollisionChannel channel,
        Func<Imp3D, bool> filter = null)
    {
        return ImpPhysics.Trace(start, end, filter);
    }

    public static bool Ray_Plane(TRay3 ray, Vector3 plane_p, Vector3 plane_n, out Vector3 hit)
    {
        hit = default;
        float denom = Vector3.Dot(ray.Direction, plane_n);
        if (MathF.Abs(denom) < 1e-7f) return false;
        float t = Vector3.Dot(plane_p - ray.Position, plane_n) / denom;
        if (t < 0f) return false;
        hit = ray.Position + ray.Direction * t;
        return true;
    }

    public static bool Ray_AABB(TRay3 ray, Vector3 min, Vector3 max, out float t)
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

    public static bool Ray_Comp3D(TRay3 ray, Imp3D c, out float t, out Vector3 hit)
    {
        t = 0f;
        hit = default;
        if (c == null || !c.is_visible)
        {
            return false;
        }
        TBounds3 b = c.bounds;
        if (b.IsEmpty)
        {
            return false;
        }
        Quaternion q = GMath.Euler_2_Quat(b.rotation);
        Quaternion inv_q = Quaternion.Inverse(q);
        Vector3 h = new(
            MathF.Abs(b.size.X) * 0.5f,
            MathF.Abs(b.size.Y) * 0.5f,
            MathF.Abs(b.size.Z) * 0.5f);
        Vector3 o = Vector3.Transform(ray.Position - b.center, inv_q);
        Vector3 d = Vector3.Transform(ray.Direction, inv_q);
        if (!Ray_AABB(new TRay3(o, d), -h, h, out t))
        {
            return false;
        }
        hit = ray.Position + ray.Direction * t;
        return true;
    }
}
