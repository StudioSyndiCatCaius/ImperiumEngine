using System.Numerics;
using Engine.Assets;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using JoltPhysicsSharp;
using R3D_cs;
using Raylib_cs;

namespace Engine.Core;


public struct TTraceResult3D
{
    public bool hit;
    public Imp3D? hit_comp;
    public Vector3 hit_position;
    public Vector3 hit_normal;
    public Vector3 start_position;
    public Vector3 start_normal;
    
}

public class Imp3D : ImpComp
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar][Category("Anti Aliasing")][Config] public static AntiAliasingMode anti_aliasing_mode = AntiAliasingMode.Smaa;
    [ImpVar][Category("Anti Aliasing")][Config] public static AntiAliasingPreset anti_aliasing_preset = AntiAliasingPreset.High;
    
    [ImpVar][Category("Shadows")][Config] public static ShadowCastMode shadow_cast_mode = ShadowCastMode.OnAuto;
    [ImpVar][Category("Shadows")][Config] public static ShadowUpdateMode shadow_update_mode = ShadowUpdateMode.Continuous;

    public static bool debug_draw_bounds;
    
    static bool _aa_applied;
    static AntiAliasingMode _aa_mode;
    static AntiAliasingPreset _aa_preset;

    public static void ApplyRenderSettings()
    {
        if (_aa_applied && _aa_mode == anti_aliasing_mode && _aa_preset == anti_aliasing_preset)
            return;
        R3D.SetAntiAliasingMode(anti_aliasing_mode);
        R3D.SetAntiAliasingPreset(anti_aliasing_preset);
        _aa_mode = anti_aliasing_mode;
        _aa_preset = anti_aliasing_preset;
        _aa_applied = true;
    }
    
    
   
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    // ============================================================
    // ImpVars
    // ============================================================
    [ImpVar] public TTransform3 transform = new();
    
    [ImpVar][Category("Physics")] public bool physics_enabled = false;
    [ImpVar][Category("Physics")] public A_CollisionPreset collision_preset = A_CollisionPreset.PRESET_NONE;
    [ImpVar][Category("Physics")] public bool movement_enabled = false;
    [ImpVar][Category("Physics")] public A_MoveMode move_mode;
    
    [ImpVar][Category("Performance")] public A_RenderConfig render_config;
    [ImpVar][Category("Performance")] public A_Significance_Config significance;
    
    // ============================================================
    // Actions
    // ============================================================
    
    // first= self, second=other
    [ScriptSignal] public Action<Imp3D,Imp3D> on_overlap_begin;
    [ScriptSignal] public Action<Imp3D,Imp3D> on_overlap_end;
    
    // ============================================================
    // Vars
    // ============================================================
    
    public TTransform3 global_transform = new();
    public Quaternion world_rotation = Quaternion.Identity;
    public TBounds3 bounds;
    
    public Vector3 velocity = Vector3.Zero;
    public bool is_grounded;

    internal Vector3 _move_wish;

    TTransform3 _local_seen;
    uint _world_ver;
    uint _parent_world_ver;

    public Imp3D() { }

    protected override ECompProcess ProcessKinds => ECompProcess.Update | ECompProcess.Draw3D;

    public override void OnBegin()
    {
        base.OnBegin();
        if (physics_enabled) ImpPhysics.Ensure(this);
    }

    public override void OnEnd()
    {
        ImpPhysics.Unregister(this);
        base.OnEnd();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (physics_enabled && scene != null && scene.is_running)
            ImpPhysics.Ensure(this);
    }

    // ---------------------------------------
    // Transform (Set Global)
    // ---------------------------------------
    public void Transform_Set(TTransform3 t, bool global = true)
    {
        if (global) global_transform = t; else transform = t;
        Correct_Transform(global, true);
    }

    public void Position_Set(Vector3 position, bool global = true)
    {
        if (global) global_transform.position = position; else transform.position = position;
        Correct_Transform(global, true);
    }

    public void Rotation_Set(Vector3 rotation, bool global = true)
    {
        if (global)global_transform.rotation = rotation; else transform.rotation = rotation;
        Correct_Transform(global, true);
    }

    public void Scale_Set(Vector3 scale, bool global = true)
    {
        if (global) global_transform.scale = scale; else transform.scale = scale;
        Correct_Transform(global, true);
    }

    protected override void Transform_Refresh()
    {
        Imp3D parent3d = parent as Imp3D;
        uint parent_ver = parent3d != null ? parent3d._world_ver : 0;
        bool local_changed = transform.position != _local_seen.position
            || transform.rotation != _local_seen.rotation
            || transform.scale != _local_seen.scale;
        if (_world_ver != 0 && !local_changed && parent_ver == _parent_world_ver)
            return;
        _local_seen = transform;
        Correct_Transform(false, false);
    }

    void Correct_Transform(bool global_changed, bool push_children)
    {
        Imp3D parent3d = parent as Imp3D;
        if (parent3d == null)
        {
            if (global_changed) transform = global_transform;
            else global_transform = transform;
        }
        else if (global_changed) transform = TTransform3.Subtract(global_transform, parent3d.global_transform, true);
        else global_transform = TTransform3.Add(parent3d.global_transform, transform, true);
        world_rotation = GMath.Euler_2_Quat(global_transform.rotation);
        bounds = Bounds_Cache();
        _local_seen = transform;
        _parent_world_ver = parent3d != null ? parent3d._world_ver : 0;
        _world_ver++;
        if (!push_children) return;
        for (int i = 0; i < children.Count; i++)
            if (children[i] is Imp3D c3) c3.Correct_Transform(false, true);
    }
    
    // ---------------------------------------
    // Camera / ViewTarget
    // ---------------------------------------

    public virtual bool ViewTarget_Enabled() { return false;}

    public virtual Camera ViewTarget_GetData()
    {
        return Camera_FromTransform(global_transform, 60.0f, 0.1f, 1000.0f, false);
    }

    // R3D derives a camera's target/up from its quaternion using a Y-up basis (forward -Z, up +Y),
    // which doesn't match our +X-forward world. Hand it explicit vectors instead of a raw quaternion.
    protected static Camera Camera_FromTransform(TTransform3 t, float fov, float near, float far, bool orthographic)
    {
        Camera cam = new()
        {
            Position = t.position,
            Fovy = fov,
            NearPlane = near,
            FarPlane = far,
            CullMask = Layer.All,
            Projection = orthographic ? Projection.Orthographic : Projection.Perspective,
        };
        Quaternion q = GMath.Euler_2_Quat(t.rotation);
        R3D.CameraLookAt(ref cam,
            t.position + Vector3.Transform(GMath.WORLD_FORWARD, q),
            Vector3.Transform(GMath.WORLD_UP, q));
        return cam;
    }
    // ---------------------------------------
    // Drawing
    // ---------------------------------------
    public virtual bool Drawing_ShadowsEnabled()
    {
        if (render_config != null) return render_config.cast_shadows;
        return false;
    }
    
    
    // ---------------------------------------
    // Physics
    // ---------------------------------------
    public virtual TBounds3 Bounds_Cache() { return new(); }

    public override void OnDrawDebug(double dt, bool drawing_3d)
    {
        if (!drawing_3d || !debug_draw_bounds) return;
        TBounds3 b = bounds.IsEmpty ? Bounds_Cache() : bounds;
        if (b.IsEmpty) return;
        float m = MathF.Min(b.size.X, MathF.Min(b.size.Y, b.size.Z));
        float thick = Math.Clamp(m * 0.015f, 0.008f, 0.03f);
        TTransform3 t = new()
        {
            position = b.center,
            rotation = b.rotation,
            scale = Vector3.One,
        };
        G3D.Draw3D_Box(t, b.size, thick, new Color(48, 220, 96, 210));
    }
    
    public virtual Shape? Phys_GetShape()
    {
        TBounds3 b = bounds.IsEmpty ? Bounds_Cache() : bounds;
        if (b.IsEmpty) return null;
        return ImpPhysics.MakeBox(b.size * 0.5f);
    }
    
    public void Phys_Move(Vector3 axis, float scale = 1.0f)
    {
        Vector3 wish = axis * scale;
        wish.Y = 0f;
        _move_wish = wish;
    }
    
    //applies movement to velocity, but does so relative to an input rotation. E.G., commonly you feed in camera rotation (or ImpPlayer.control_rotation)
    public void Phys_MoveByRot(Vector3 axis, Vector3 rotation)
    {
        Vector3 wish = GMath.V3_Rotate(axis, rotation);
        wish.Y = 0f;
        _move_wish = wish;
    }

    public void Phys_Launch(Vector3 vector, bool override_velocity_H = false, bool override_velocity_v = false)
    {
        if (override_velocity_H)
        {
            velocity.X = vector.X;
            velocity.Z = vector.Z;
        }
        else
        {
            velocity.X += vector.X;
            velocity.Z += vector.Z;
        }
        if (override_velocity_v) velocity.Y = vector.Y;
        else velocity.Y += vector.Y;
    }
    
    // -- VIRTUALS
    
    public virtual void OnOverlapBegin(Imp3D other) { }
    public virtual void OnOverlapEnd(Imp3D other) { }
    
    // ---------------------------------------
    // EDITOR
    // ---------------------------------------
    // OBB aligned to this object's rotation, large enough to contain this node
    // and every descendant Imp3D's local bounds.
    public TBounds3 Bounds_Encompass()
    {
        Quaternion q = world_rotation;
        Quaternion inv = Quaternion.Inverse(q);
        Vector3 origin = global_transform.position;
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool any = false;
        Span<Vector3> corners = stackalloc Vector3[8];
        EncompassWalk(this, true, origin, inv, corners, ref min, ref max, ref any);
        if (!any)
        {
            Vector3 sc = global_transform.scale;
            return new TBounds3
            {
                center = origin,
                size = new Vector3(
                    MathF.Max(0.5f * MathF.Abs(sc.X), 0.05f),
                    MathF.Max(0.5f * MathF.Abs(sc.Y), 0.05f),
                    MathF.Max(0.5f * MathF.Abs(sc.Z), 0.05f)),
                rotation = global_transform.rotation,
            };
        }
        Vector3 size = max - min;
        if (size.X < 0.05f) size.X = 0.05f;
        if (size.Y < 0.05f) size.Y = 0.05f;
        if (size.Z < 0.05f) size.Z = 0.05f;
        return new TBounds3
        {
            center = origin + Vector3.Transform((min + max) * 0.5f, q),
            size = size,
            rotation = global_transform.rotation,
        };
    }

    static void EncompassWalk(ImpComp c, bool is_root, Vector3 origin, Quaternion inv,
        Span<Vector3> corners, ref Vector3 min, ref Vector3 max, ref bool any)
    {
        if (c == null || (!is_root && !c.is_visible)) return;
        if (c is Imp3D o3)
        {
            TBounds3 b = o3.bounds.IsEmpty ? o3.Bounds_Cache() : o3.bounds;
            if (!b.IsEmpty)
            {
                b.Corners(corners);
                for (int k = 0; k < 8; k++)
                {
                    Vector3 local = Vector3.Transform(corners[k] - origin, inv);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
                any = true;
            }
        }
        for (int i = 0; i < c.children.Count; i++)
            EncompassWalk(c.children[i], false, origin, inv, corners, ref min, ref max, ref any);
    }
}
