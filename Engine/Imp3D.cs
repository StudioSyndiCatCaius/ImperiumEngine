using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;

public class Imp3D : ImpComp
{
    // #######################################################################################################################
    // #######################################################################################################################
    // Static
    // #######################################################################################################################
    // #######################################################################################################################
    
    public static void Draw_Line(Vector3 start, Vector3 end, float thickness, Color color)
    {
        Raylib.DrawLine3D(start, end, color);
    }

    // ---------------------------------
    // Trace
    // ---------------------------------

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

    public static bool Pickable(Imp3D c) => c != null && !c.IsGroupPivot;

    public static void Comp3D_LocalBounds(Imp3D c, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(-0.2f);
        max = new Vector3(0.2f);
        if (c is C3_Collider col)
        {
            col.Shape_Local(out Vector3 size, out Vector3 center);
            Vector3 half = size * 0.5f;
            min = center - half;
            max = center + half;
            return;
        }
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

    public static bool Ray_Comp3D(Ray ray, Imp3D c, out float t, out Vector3 hit)
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

    public static Imp3D Pick_Comp3D(ImpComp root, Ray ray, out Vector3 hit)
    {
        Imp3D best = null;
        Vector3 best_hit = default;
        float best_t = float.MaxValue;
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible) return;
            if (n is Imp3D c3 && n is not C3_Gizmo && Pickable(c3))
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

    private static readonly List<Imp3D> _occ = new();

    public static void Occ_Gather(ImpComp root)
    {
        _occ.Clear();
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible) return;
            if (n is Imp3D c3 && n is not C3_Gizmo && Pickable(c3)) _occ.Add(c3);
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

    public static void Comp3D_WorldCorners(Imp3D c, Vector3[] corners)
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

    public override void OnBegin()
    {
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
            transform = value;
        else
            transform = LocalFromWorld(p.Transform_Get(true), value);
        _phys_dirty = true;
    }
    [ScriptCall]
    public TTransform3 Transform_Get(bool world_space = false)
    {
        if (!world_space || parent is not Imp3D p)
            return transform;
        return WorldFromLocal(p.Transform_Get(true), transform);
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
            return;
        }

        var parent_w = p.Transform_Get(true);
        var q = EulerToQuat(parent_w.rotation);
        Vector3 inv_s = new(
            parent_w.scale.X != 0 ? 1f / parent_w.scale.X : 0,
            parent_w.scale.Y != 0 ? 1f / parent_w.scale.Y : 0,
            parent_w.scale.Z != 0 ? 1f / parent_w.scale.Z : 0);
        transform.position = Vector3.Transform(position - parent_w.position, Quaternion.Inverse(q)) * inv_s;
        _phys_dirty = true;
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
            return;
        }

        var parent_q = EulerToQuat(p.Transform_Get(true).rotation);
        transform.rotation = QuatToEuler(Quaternion.Inverse(parent_q) * EulerToQuat(rotation));
        _phys_dirty = true;
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
            return;
        }

        var ps = p.Transform_Get(true).scale;
        transform.scale = new Vector3(
            ps.X != 0 ? scale.X / ps.X : 0,
            ps.Y != 0 ? scale.Y / ps.Y : 0,
            ps.Z != 0 ? scale.Z / ps.Z : 0);
        _phys_dirty = true;
    }
    [ScriptCall]
    static TTransform3 WorldFromLocal(TTransform3 parent, TTransform3 local)
    {
        var pq = EulerToQuat(parent.rotation);
        return new TTransform3
        {
            position = parent.position + Vector3.Transform(local.position * parent.scale, pq),
            rotation = QuatToEuler(pq * EulerToQuat(local.rotation)),
            scale = parent.scale * local.scale
        };
    }
    [ScriptCall]
    static TTransform3 LocalFromWorld(TTransform3 parent, TTransform3 world)
    {
        var pq = EulerToQuat(parent.rotation);
        Vector3 inv_s = new(
            parent.scale.X != 0 ? 1f / parent.scale.X : 0,
            parent.scale.Y != 0 ? 1f / parent.scale.Y : 0,
            parent.scale.Z != 0 ? 1f / parent.scale.Z : 0);
        return new TTransform3
        {
            position = Vector3.Transform(world.position - parent.position, Quaternion.Inverse(pq)) * inv_s,
            rotation = QuatToEuler(Quaternion.Inverse(pq) * EulerToQuat(world.rotation)),
            scale = world.scale * inv_s
        };
    }

    // X=pitch, Y=yaw, Z=roll (degrees)
    static Quaternion EulerToQuat(Vector3 euler_deg)
    {
        float deg2rad = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(euler_deg.Y * deg2rad, euler_deg.X * deg2rad, euler_deg.Z * deg2rad);
    }

    static Vector3 QuatToEuler(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        float sinp = 2f * (q.W * q.X - q.Z * q.Y);
        float pitch, yaw, roll;
        if (MathF.Abs(sinp) >= 1f)
            pitch = MathF.CopySign(MathF.PI / 2f, sinp);
        else
            pitch = MathF.Asin(sinp);
        yaw = MathF.Atan2(2f * (q.W * q.Y + q.Z * q.X), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        roll = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));
        float rad2deg = 180f / MathF.PI;
        return new Vector3(pitch * rad2deg, yaw * rad2deg, roll * rad2deg);
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
        Vector3 world = Vector3.Transform(dir, ImpMath.EulerToQuat(rot_axis));
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
            if (v_up < 0f)
            {
                velocity -= up * v_up;
            }
        }
        else if (mode.gravity_enabled)
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

    static Vector3 GravityDir(A_MoveMode mode)
    {
        Vector3 g = mode.gravity_dir;
        if (g.LengthSquared() < 1e-8f)
        {
            return new Vector3(0f, -1f, 0f);
        }
        return Vector3.Normalize(g);
    }
    
};