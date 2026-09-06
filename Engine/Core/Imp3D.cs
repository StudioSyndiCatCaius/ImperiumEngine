using System.Numerics;
using Engine.Assets;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
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
    //==================================================================================================
    // STATIC
    //==================================================================================================
    [ImpVar][Category("Anti Aliasing")][Config] public static AntiAliasingMode anti_aliasing_mode = AntiAliasingMode.Smaa;
    [ImpVar][Category("Anti Aliasing")][Config] public static AntiAliasingPreset anti_aliasing_preset = AntiAliasingPreset.High;
    
    [ImpVar][Category("Shadows")][Config] public static ShadowCastMode shadow_cast_mode = ShadowCastMode.OnAuto;
    [ImpVar][Category("Shadows")][Config] public static ShadowUpdateMode shadow_update_mode = ShadowUpdateMode.Continuous;
    
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
    
    
   
    //==================================================================================================
    // CLASS
    //==================================================================================================
    [ImpVar] public TTransform3 transform = new();
    
    [ImpVar][Category("Physics")] public bool physics_enabled = false;
    [ImpVar][Category("Physics")] public bool movement_enabled = false;
    [ImpVar][Category("Physics")] public A_MoveMode move_mode;
    
    [ImpVar][Category("Performance")] public A_RenderConfig render_config;
    [ImpVar][Category("Performance")] public A_Significance_Config significance;
    
    public TTransform3 global_transform = new();
    public Quaternion world_rotation = Quaternion.Identity;
    public TBounds3 bounds;
    
    public Vector3 velocity = Vector3.Zero;

    TTransform3 _local_seen;
    uint _world_ver;
    uint _parent_world_ver;

    public Imp3D() { }

    protected override ECompProcess ProcessKinds => ECompProcess.Update | ECompProcess.Draw3D;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (physics_enabled)
        {
            if (movement_enabled)
            {
                
            }
        }
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
    
    public object Phys_GetShape()
    {
        return null;
    }
    
    public void Phys_Move(Vector3 axis, float scale = 1.0f)
    {
        
    }
    
    public void Phys_MoveByRot(Vector3 axis, Vector3 rotation)
    {
        
    }

    public void Phys_Launch(Vector3 vector, bool override_velocity_H = false, bool override_velocity_v = false)
    {
        
    }
    
    // ---------------------------------------
    // EDITOR
    // ---------------------------------------
}
