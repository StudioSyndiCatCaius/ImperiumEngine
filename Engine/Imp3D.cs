using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;

namespace ImperiumEngine;

public class Imp3D : ImpComp
{
    // #######################################################################################################################
    // #######################################################################################################################
    // Static
    // #######################################################################################################################
    // #######################################################################################################################

    [ImpVar][Config] public static AntiAliasingMode anti_aliasing_mode = AntiAliasingMode.Smaa;
    [ImpVar][Config] public static AntiAliasingPreset anti_aliasing_preset = AntiAliasingPreset.High;

    static int _r3d_res_w;
    static int _r3d_res_h;

    public static void RefreshGraphics()
    {
        R3D.SetAntiAliasingMode(anti_aliasing_mode);
        R3D.SetAntiAliasingPreset(anti_aliasing_preset);
    }

    // R3D renders to a fixed internal framebuffer (Init / SetResolution), then
    // nearest-upscales into the window or viewport RT. Skip realloc when size
    // is unchanged — SetResolution stalls.
    public static void Resolution_Sync(int w, int h)
    {
        if (w < 1)
        {
            w = 1;
        }
        if (h < 1)
        {
            h = 1;
        }
        if (w == _r3d_res_w && h == _r3d_res_h)
        {
            return;
        }
        R3D.SetResolution(w, h);
        _r3d_res_w = w;
        _r3d_res_h = h;
    }

    public static void Resolution_Track(int w, int h)
    {
        if (w < 1)
        {
            w = 1;
        }
        if (h < 1)
        {
            h = 1;
        }
        _r3d_res_w = w;
        _r3d_res_h = h;
    }
    
    // ---------------------------------------------------------------------------------------------------
    // Draw
    // ---------------------------------------------------------------------------------------------------

    static R3D_cs.Mesh _line_mesh;
    static bool _line_mesh_ready;

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

        Material mat = R3D.GetDefaultMaterial();
        AlbedoMap alb = mat.Albedo;
        alb.Color = color;
        mat.Albedo = alb;
        mat.Unlit = true;
        R3D.DrawMeshEx(_line_mesh, mat, mid, rot, new Vector3(thickness, len, thickness));
        return new TBounds3
        {
            center = mid,
            size = new Vector3(thickness, len, thickness),
            rotation = ImpMath.Quat_2_Euler(rot),
        };
    }
    
    public static TBounds3 Draw3D_Box(TTransform3 transform, Vector3 bounds, float thickness =0.4f, Color color = default)
    {
        if (color.A == 0)
        {
            color = Color.White;
        }
        Quaternion rot = ImpMath.Euler_2_Quat(transform.rotation);
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
        Quaternion q = ImpMath.Euler_2_Quat(transform.rotation);
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
        if (radius < 0.001f)
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
        Quaternion q = ImpMath.Euler_2_Quat(transform.rotation);
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
        Quaternion q = ImpMath.Euler_2_Quat(transform.rotation);
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

    static void DrawWireLine(Vector3 origin, Quaternion rot, Vector3 a, Vector3 b, float thickness, Color color)
    {
        Draw3D_Line(origin + Vector3.Transform(a, rot), origin + Vector3.Transform(b, rot), thickness, color);
    }

    static void DrawWireCircle(Vector3 origin, Quaternion rot, Vector3 center, Vector3 axis_a, Vector3 axis_b, int slices, float thickness, Color color)
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

    static void DrawWireHemi(Vector3 origin, Quaternion rot, Vector3 center, float rx, float ry, float rz, float y_sign, int steps, float thickness, Color color)
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
    
    public static TBounds3 Draw3D_Mesh(A_Mesh mesh, TTransform3 transform, bool cast_shadow = true)
    {
        if (mesh == null)
        {
            return TBounds3.ZERO;
        }
        mesh.Draw(transform.position, ImpMath.Euler_2_Quat(transform.rotation), transform.scale, null, cast_shadow);
        return mesh.Bounds_Get(transform);
    }


    // ---------------------------------------------------------------------------------------------------
    // Trace
    // ---------------------------------------------------------------------------------------------------

    public static TTraceResult3D Trace_Line(Vector3 start, Vector3 end, ECollisionChannel channel,
        Func<Imp3D, bool> filter = null)
    {
        ImpGame g = ImpGame.current;
        if (g == null || g.phys == null)
        {
            return default;
        }
        return g.phys.Trace_Line(start, end, channel, filter);
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

    public static bool Ray_Comp3D(Ray ray, Imp3D c, out float t, out Vector3 hit)
    {
        t = 0f;
        hit = default;
        if (c == null || !c.is_visible)
        {
            return false;
        }
        TBounds3 b = c.Bounds_Get();
        if (b.IsEmpty)
        {
            return false;
        }
        Quaternion q = ImpMath.Euler_2_Quat(b.rotation);
        Quaternion inv_q = Quaternion.Inverse(q);
        Vector3 h = new(
            MathF.Abs(b.size.X) * 0.5f,
            MathF.Abs(b.size.Y) * 0.5f,
            MathF.Abs(b.size.Z) * 0.5f);
        Vector3 o = Vector3.Transform(ray.Position - b.center, inv_q);
        Vector3 d = Vector3.Transform(ray.Direction, inv_q);
        if (!Ray_AABB(new Ray(o, d), -h, h, out t))
        {
            return false;
        }
        hit = ray.Position + ray.Direction * t;
        return true;
    }

    public static Imp3D Select(ImpComp root, Ray ray, out Vector3 hit)
    {
        List<Imp3D> comps = new();
        List<float> ts = new();
        List<Vector3> pts = new();
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible)
            {
                return;
            }
            if (n is Imp3D c3 && n is not C3_Gizmo)
            {
                if (Ray_Comp3D(ray, c3, out float t, out Vector3 h))
                {
                    comps.Add(c3);
                    ts.Add(t);
                    pts.Add(h);
                }
            }
            int nchild = n.children.Count;
            for (int i = 0; i < nchild; i++)
            {
                Walk(n.children[i]);
            }
        }
        Walk(root);

        Imp3D best = null;
        Vector3 best_hit = default;
        float best_t = float.MaxValue;
        float best_vol = float.MaxValue;
        int n_hits = comps.Count;
        for (int i = 0; i < n_hits; i++)
        {
            Imp3D c = comps[i];
            bool covered = false;
            for (int j = 0; j < n_hits; j++)
            {
                if (i == j)
                {
                    continue;
                }
                ImpComp p = comps[j].parent;
                while (p != null)
                {
                    if (p == c)
                    {
                        covered = true;
                        break;
                    }
                    p = p.parent;
                }
                if (covered)
                {
                    break;
                }
            }
            if (covered)
            {
                continue;
            }
            TBounds3 b = c.cached_bounds;
            float vol = MathF.Abs(b.size.X) * MathF.Abs(b.size.Y) * MathF.Abs(b.size.Z);
            float t = ts[i];
            bool better = t < best_t - 1e-4f;
            if (!better && MathF.Abs(t - best_t) <= 1e-4f && vol < best_vol)
            {
                better = true;
            }
            if (better)
            {
                best_t = t;
                best_vol = vol;
                best = c;
                best_hit = pts[i];
            }
        }
        hit = best_hit;
        return best;
    }

    private static readonly List<Imp3D> _occ = new();

    public static void Occ_Gather(ImpComp root)
    {
        _occ.Clear();
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible)
            {
                return;
            }
            if (n is Imp3D c3 && n is not C3_Gizmo)
            {
                TBounds3 b = c3.Bounds_Get();
                if (!b.IsEmpty)
                {
                    _occ.Add(c3);
                }
            }
            int nchild = n.children.Count;
            for (int i = 0; i < nchild; i++)
            {
                Walk(n.children[i]);
            }
        }
        Walk(root);
    }

    public static bool Occ_Point(Camera3D cam, Vector3 world, float bias = 0.03f)
    {
        Vector3 dir = world - cam.Position;
        float dist = dir.Length();
        if (dist < 1e-4f)
        {
            return false;
        }
        Ray ray = new(cam.Position, dir / dist);
        for (int i = 0; i < _occ.Count; i++)
        {
            if (!Ray_Comp3D(ray, _occ[i], out float t, out _))
            {
                continue;
            }
            if (t > 0f && t < dist - bias)
            {
                return true;
            }
        }
        return false;
    }

    // #######################################################################################################################
    // #######################################################################################################################
    // Class
    // #######################################################################################################################
    // #######################################################################################################################
    
    [Category("Transform")] [ImpVar] public TTransform3 transform = new();
    public A_CollisionPreset collision_preset = A_CollisionPreset.PRESET_NONE;

    public Vector3 velocity;
    public bool is_grounded;
    Vector3 _wish;
    internal bool _phys_dirty;
    internal Vector3 _phys_scale = Vector3.One;
    
    // Cached every update, parent first, so children/parents can read world without a tree walk.
    public TTransform3 global_transform = new();
    public TBounds3 cached_bounds;
    static uint _cache_epoch = 1;
    uint _e_cached;

    public static void Cache_Invalidate()
    {
        if (++_cache_epoch == 0)
        {
            _cache_epoch = 1;
        }
    }

    public void Cache_Refresh(bool force = false)
    {
        if (!force && _e_cached == _cache_epoch)
        {
            return;
        }
        TTransform3 world;
        if (parent is Imp3D p)
        {
            p.Cache_Refresh();
            world = WorldFromLocal(p.global_transform, transform);
        }
        else
        {
            world = transform;
        }
        bool moved =
            global_transform.position != world.position
            || global_transform.rotation != world.rotation
            || global_transform.scale != world.scale;
        global_transform = world;
        _e_cached = _cache_epoch;
        if (force && moved)
        {
            int n = children.Count;
            for (int i = 0; i < n; i++)
            {
                if (children[i] is Imp3D c)
                {
                    c.Cache_Dirty();
                }
            }
        }
        cached_bounds = Bounds_Calc();
    }

    void Cache_Dirty()
    {
        _e_cached = 0;
        int n = children.Count;
        for (int i = 0; i < n; i++)
        {
            if (children[i] is Imp3D c)
            {
                c.Cache_Dirty();
            }
        }
    }

    public override void OnBegin()
    {
        Cache_Refresh();
        if (physics_enabled)
        {
            Phys_Register();
        }
        base.OnBegin();
    }

    public override void OnEnd()
    {
        base.OnEnd();
        Phys_Unregister();
    }

    protected override void OnDestroy()
    {
        Phys_Unregister();
        base.OnDestroy();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (physics_enabled)
        {
            Update_Physics(dt);
            if (movement_enabled)
            {
                Update_Movement(dt);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Transform
    // ---------------------------------------------------------------------------------------------------------------

    [ScriptCall]
    public void Transform_Set(TTransform3 value, bool world_space = false)
    {
        if (!world_space || parent is not Imp3D p)
        {
            transform = value;
        }
        else
        {
            p.Cache_Refresh();
            transform = LocalFromWorld(p.global_transform, value);
        }
        _phys_dirty = true;
        Cache_Dirty();
        Cache_Refresh();
    }
    [ScriptCall]
    public TTransform3 Transform_Get(bool world_space = false)
    {
        if (!world_space || parent is not Imp3D p)
        {
            return transform;
        }
        p.Cache_Refresh();
        return WorldFromLocal(p.global_transform, transform);
    }
    [ScriptCall]
    public Vector3 Position_Get(bool world_space = false)
    {
        return Transform_Get(world_space).position;
    }
    [ScriptCall]
    public void Position_Set(Vector3 position, bool world_space = false)
    {
        if (!world_space || parent is not Imp3D p)
        {
            transform.position = position;
            _phys_dirty = true;
            Cache_Dirty();
            Cache_Refresh();
            return;
        }

        p.Cache_Refresh();
        var parent_w = p.global_transform;
        var q = ImpMath.Euler_2_Quat(parent_w.rotation);
        Vector3 inv_s = new(
            parent_w.scale.X != 0 ? 1f / parent_w.scale.X : 0,
            parent_w.scale.Y != 0 ? 1f / parent_w.scale.Y : 0,
            parent_w.scale.Z != 0 ? 1f / parent_w.scale.Z : 0);
        transform.position = Vector3.Transform(position - parent_w.position, Quaternion.Inverse(q)) * inv_s;
        _phys_dirty = true;
        Cache_Dirty();
        Cache_Refresh();
    }
    [ScriptCall]
    public Vector3 Rotation_Get(bool world_space = false)
    {
        return Transform_Get(world_space).rotation;
    }
    [ScriptCall]
    public void Rotation_Set(Vector3 rotation, bool world_space = false)
    {
        if (!world_space || parent is not Imp3D p)
        {
            transform.rotation = rotation;
            _phys_dirty = true;
            Cache_Dirty();
            Cache_Refresh();
            return;
        }

        p.Cache_Refresh();
        var parent_q = ImpMath.Euler_2_Quat(p.global_transform.rotation);
        transform.rotation = ImpMath.Quat_2_Euler(Quaternion.Inverse(parent_q) * ImpMath.Euler_2_Quat(rotation));
        _phys_dirty = true;
        Cache_Dirty();
        Cache_Refresh();
    }
    [ScriptCall]
    public Vector3 Scale_Get(bool world_space = false)
    {
        return Transform_Get(world_space).scale;
    }
    [ScriptCall]
    public void Scale_Set(Vector3 scale, bool world_space = false)
    {
        if (!world_space || parent is not Imp3D p)
        {
            transform.scale = scale;
            _phys_dirty = true;
            Cache_Dirty();
            Cache_Refresh();
            return;
        }

        p.Cache_Refresh();
        var ps = p.global_transform.scale;
        transform.scale = new Vector3(
            ps.X != 0 ? scale.X / ps.X : 0,
            ps.Y != 0 ? scale.Y / ps.Y : 0,
            ps.Z != 0 ? scale.Z / ps.Z : 0);
        _phys_dirty = true;
        Cache_Dirty();
        Cache_Refresh();
    }
    [ScriptCall]
    static TTransform3 WorldFromLocal(TTransform3 parent, TTransform3 local)
    {
        var pq = ImpMath.Euler_2_Quat(parent.rotation);
        return new TTransform3
        {
            position = parent.position + Vector3.Transform(local.position * parent.scale, pq),
            rotation = ImpMath.Quat_2_Euler(pq * ImpMath.Euler_2_Quat(local.rotation)),
            scale = parent.scale * local.scale
        };
    }
    [ScriptCall]
    static TTransform3 LocalFromWorld(TTransform3 parent, TTransform3 world)
    {
        var pq = ImpMath.Euler_2_Quat(parent.rotation);
        Vector3 inv_s = new(
            parent.scale.X != 0 ? 1f / parent.scale.X : 0,
            parent.scale.Y != 0 ? 1f / parent.scale.Y : 0,
            parent.scale.Z != 0 ? 1f / parent.scale.Z : 0);
        return new TTransform3
        {
            position = Vector3.Transform(world.position - parent.position, Quaternion.Inverse(pq)) * inv_s,
            rotation = ImpMath.Quat_2_Euler(Quaternion.Inverse(pq) * ImpMath.Euler_2_Quat(world.rotation)),
            scale = world.scale * inv_s
        };
    }
    
    // ---------------------------------------------------------------------------------------------------------------
    // Bounds
    // ---------------------------------------------------------------------------------------------------------------
    public TBounds3 Bounds_Get()
    {
        Cache_Refresh();
        return cached_bounds;
    }

    protected virtual TBounds3 Bounds_Calc()
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool any = false;
        Span<Vector3> corners = stackalloc Vector3[8];
        int n = children.Count;
        for (int i = 0; i < n; i++)
        {
            if (children[i] is not Imp3D c)
            {
                continue;
            }
            if (!c.is_visible || c is C3_Gizmo)
            {
                continue;
            }
            TBounds3 b = c.Bounds_Get();
            if (b.IsEmpty)
            {
                continue;
            }
            b.Corners(corners);
            for (int k = 0; k < 8; k++)
            {
                min = Vector3.Min(min, corners[k]);
                max = Vector3.Max(max, corners[k]);
            }
            any = true;
        }
        if (!any)
        {
            return TBounds3.ZERO;
        }
        return new TBounds3
        {
            center = (min + max) * 0.5f,
            size = max - min,
            rotation = Vector3.Zero,
        };
    }
    
    // ---------------------------------------------------------------------------------------------------------------
    // Physics / Movement
    // ---------------------------------------------------------------------------------------------------------------
    
    [ImpVar][Category("Physics")] public bool physics_enabled;
    [ImpVar][Category("Physics")] public bool movement_enabled; // runs the movement update only works when physics are enabled
    [ImpVar][Category("Physics")] public A_MoveMode move_mode;

    public void Phys_Register()
    {
        ImpGame g = game_owner;
        if (g == null)
        {
            return;
        }
        g.Phys_Get().Register(this);
    }

    public void Phys_Unregister()
    {
        ImpGame g = game_owner;
        if (g == null || g.phys == null)
        {
            return;
        }
        g.phys.Unregister(this);
    }

    public virtual JoltPhysicsSharp.Shape Phys_MakeShape(Vector3 world_scale)
    {
        return null;
    }

    public virtual Vector3 Phys_ShapeOffset(Vector3 world_scale)
    {
        return Vector3.Zero;
    }

    public virtual float Phys_SupportRadius(Vector3 world_scale)
    {
        return 0.1f;
    }

    //equip of pawn Movement Input in UE
    public void Phys_Move(Vector3 dir, double scale)
    {
        if (!movement_enabled)
        {
            return;
        }
        _wish += dir * (float)scale;
    }

    //similar movement BUT is relative to rotation axis. E.G. you can input a camera's global rotaion to make sure movement is relative to camera
    public void Phys_MoveByRot(Vector3 dir, double scale, Vector3 rot_axis)
    {
        if (!movement_enabled)
        {
            return;
        }
        Vector3 world = Vector3.Transform(dir, ImpMath.Euler_2_Quat(rot_axis));
        Phys_Move(world, scale);
    }

    public void Phys_Launch(Vector3 axis, double scale, bool force_velocity_h=false, bool force_velocity_v=false)
    {
        if (!movement_enabled)
        {
            return;
        }
        A_MoveMode mode = MoveMode_Get();
        Vector3 grav = GravityDir(mode);
        Vector3 up = -grav;
        Vector3 impulse = axis * (float)scale;
        Vector3 v = velocity;
        Vector3 v_up = up * Vector3.Dot(v, up);
        Vector3 v_h = v - v_up;
        Vector3 i_up = up * Vector3.Dot(impulse, up);
        Vector3 i_h = impulse - i_up;
        if (force_velocity_h)
        {
            v_h = i_h;
        }
        else
        {
            v_h += i_h;
        }
        if (force_velocity_v)
        {
            v_up = i_up;
        }
        else
        {
            v_up += i_up;
        }
        velocity = v_h + v_up;
    }

    public virtual void Update_Physics(double dt)
    {
        if (!movement_enabled)
        {
            return;
        }
        A_MoveMode mode = MoveMode_Get();
        Vector3 grav = GravityDir(mode);
        Vector3 up = -grav;
        if (is_grounded)
        {
            float v_up = Vector3.Dot(velocity, up);
            if (v_up < 0.1f)
            {
                velocity -= up * v_up;
            }
        }
        if (mode.gravity_enabled)
        {
            velocity += grav * 9.81f * mode.gravity_scale * (float)dt;
        }
    }

    public virtual void Update_Movement(double dt)
    {
        A_MoveMode mode = MoveMode_Get();
        Vector3 grav = GravityDir(mode);
        Vector3 up = -grav;
        Vector3 wish = _wish;
        _wish = Vector3.Zero;
        wish -= up * Vector3.Dot(wish, up);
        if (wish.LengthSquared() > 1f)
        {
            wish = Vector3.Normalize(wish);
        }

        float v_up = Vector3.Dot(velocity, up);
        Vector3 v_h = velocity - up * v_up;
        Vector3 target = wish * mode.speed;
        float rate;
        if (is_grounded)
        {
            if (wish.LengthSquared() > 1e-6f)
            {
                rate = mode.acceleration;
            }
            else
            {
                rate = mode.deceleration;
            }
        }
        else
        {
            rate = mode.acceleration * mode.air_control;
            if (mode.air_friction > 0f)
            {
                v_h *= MathF.Max(0f, 1f - mode.air_friction * (float)dt);
            }
        }
        Vector3 delta_v = target - v_h;
        float delta_len = delta_v.Length();
        float max_delta = rate * (float)dt;
        if (delta_len <= max_delta || delta_len < 1e-8f)
        {
            v_h = target;
        }
        else
        {
            v_h = v_h + delta_v * (max_delta / delta_len);
        }
        Vector3 v_h_flat = v_h - up * Vector3.Dot(v_h, up);
        if (v_h_flat.LengthSquared() > mode.speed * mode.speed && mode.speed > 0f)
        {
            v_h_flat = Vector3.Normalize(v_h_flat) * mode.speed;
        }
        velocity = v_h_flat + up * v_up;

        if (mode.rotate_with_movement && v_h_flat.LengthSquared() > 1e-4f)
        {
            Vector3 flat = Vector3.Normalize(v_h_flat);
            Vector3 target_rot = new(0f, MathF.Atan2(-flat.X, -flat.Z) * (180f / MathF.PI), 0f);
            Vector3 rot = Rotation_Get(true);
            Vector3 turn = mode.velocity_rotation_rate;
            float dt_f = (float)dt;

            float StepAxis(float current, float desired, float max_delta)
            {
                if (max_delta <= 0f)
                {
                    return current;
                }
                float ad = desired - current;
                while (ad > 180f)
                {
                    ad -= 360f;
                }
                while (ad < -180f)
                {
                    ad += 360f;
                }
                if (MathF.Abs(ad) <= max_delta)
                {
                    return desired;
                }
                if (ad > 0f)
                {
                    return current + max_delta;
                }
                return current - max_delta;
            }

            rot.X = StepAxis(rot.X, target_rot.X, MathF.Abs(turn.X) * dt_f);
            rot.Y = StepAxis(rot.Y, target_rot.Y, MathF.Abs(turn.Y) * dt_f);
            rot.Z = StepAxis(rot.Z, target_rot.Z, MathF.Abs(turn.Z) * dt_f);
            Rotation_Set(rot, true);
        }
    }

    A_MoveMode MoveMode_Get()
    {
        if (move_mode != null)
        {
            return move_mode;
        }
        return A_MoveMode.DEFAULT;
    }

    internal static Vector3 GravityDir(A_MoveMode mode)
    {
        Vector3 g;
        if (mode == null)
        {
            g = new Vector3(0f, -1f, 0f);
        }
        else
        {
            g = mode.gravity_dir;
        }
        if (g.LengthSquared() < 1e-8f)
        {
            return new Vector3(0f, -1f, 0f);
        }
        return Vector3.Normalize(g);
    }
    
    // ---------------------------------------------------------------------------------------------------
    // Camera
    // ---------------------------------------------------------------------------------------------------
    static Camera _invalid_camera = new();
    public virtual Camera Camera_GetData() { return _invalid_camera; }
    public virtual bool Camera_IsValid() { return false; }
    
};