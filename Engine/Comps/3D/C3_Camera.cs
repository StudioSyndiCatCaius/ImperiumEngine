using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Camera : Imp3D
{
    [ImpVar] public A_CameraConfig config = new();
    [ImpVar] public Imp3D look_target;
    [ImpVar] public bool follow_pawn_when_view_target = true;

    public C3_SpringArm camera_boom = new();

    static A_Mesh _cam_mesh = A_Mesh.UTIL_CAMERA;
    

    public C3_Camera()
    {
        physics_enabled = false;
        Child_Add(camera_boom);
    }

    public A_CameraConfig Config_Get()
    {
        if (config != null)
        {
            return config;
        }
        return A_CameraConfig.CAM_THIRDPERSON;
    }

    public TTransform3 Transform_GetForCamera()
    {
        Boom_ApplyConfig();
        if (camera_boom != null)
        {
            return camera_boom.Socket_WorldTransform();
        }
        return Transform_Get(true);
    }

    public override void OnBegin()
    {
        base.OnBegin();
        Boom_ApplyConfig();
        if (camera_boom != null)
        {
            camera_boom.ResetLag();
        }
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        
        //look to target
        if(look_target != null)
        {
            Vector3 _targ=ImpMath.V3_Interp(global_transform.position, look_target.global_transform.position, dt, config.look_speed);
            Rotation_Set(_targ, true);
        }
        
    }

    public override void _Notify_AsViewTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsViewTarget(player, notify, dt);
        switch (notify)
        {
            case ENotifyGeneric.Begin:
                break;
            case ENotifyGeneric.End:
                break;
            case ENotifyGeneric.Update:
                if (follow_pawn_when_view_target)
                {
                    Imp3D pawn = Pawn_Get();
                    if (pawn != null)
                    {
                        Vector3 _offset=new(0f, 1.5f, 0f);
                        Position_Set(pawn.global_transform.position+_offset, true);
                    }
                }
                break;
            
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // INPUT
    // ---------------------------------------------------------------------------------------------------------

    public override void Input_Update(ImpPlayer player, TLabel iaction, double dt, Vector3 axis)
    {
        base.Input_Update(player, iaction, dt, axis);
        A_CameraConfig cfg = Config_Get();
        
        // ----------------------
        // ROTATE
        // ----------------------
        float rotate_sensitivity = 2.5f;
        float rotate_rate=rotate_sensitivity*(float)dt;
        if (iaction == "_Rotate")
        {
            if ((!cfg.enable_rotate_H && !cfg.enable_rotate_V) || look_target != null)
            {
                return;
            }

            if (cfg.enable_rotate_H)
            {
                transform.rotation.X += axis.X * rotate_rate;
            }
            if (cfg.enable_rotate_V)
            {
                transform.rotation.Y += axis.Y * rotate_rate;
            }
            
            return;
        }
        // ----------------------
        // INPUT
        // ----------------------
        Imp3D pawn = player.pawn;
        if (pawn == null && iaction=="_Move" && cfg.enable_move)
        {
            return;
        }
        //Vector3 local = new(axis.Z, axis.Y, -axis.X);
        //Vector3 yaw_only = new(0f, Rotation_Get(true).Y, 0f);
        pawn.Phys_MoveByRot(axis, 1, global_transform.rotation*Vector3.UnitY);
    }
    

    public override void OnDraw3D(double dt, EDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (!flags.HasFlag(EDrawFlags.Editor))
        {
            return;
        }

        A_CameraConfig cfg = Config_Get();
        TTransform3 t = Transform_Get(true);
        TTransform3 cam_xf = Transform_GetForCamera();
        Vector3 fin = cam_xf.position;
        bool selected = flags.HasFlag(EDrawFlags.Selected);
        Color col = Color.White;
        if (selected)
        {
            col = ImpGizmo.ColSelect;
        }
        Draw3D_Line(t.position, fin, 0.03f, col);

        if (selected)
        {
            Quaternion rot = ImpMath.Euler_2_Quat(cam_xf.rotation);
            Vector3 fwd = Vector3.Transform(-Vector3.UnitZ, rot);
            Vector3 right = Vector3.Transform(Vector3.UnitX, rot);
            Vector3 up = Vector3.Transform(Vector3.UnitY, rot);
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();
            float aspect = 16f / 9f;
            if (sh > 0)
            {
                aspect = (float)sw / (float)sh;
            }
            float far_d = 2.5f;
            float half_v;
            float half_h;
            if (cfg.view_mode == ECameraViewMode.Orthographic)
            {
                half_v = (float)cfg.fov * 0.5f;
                if (half_v < 0.01f)
                {
                    half_v = 0.01f;
                }
                half_h = half_v * aspect;
            }
            else
            {
                float fov = (float)cfg.fov;
                if (fov < 1f)
                {
                    fov = 60f;
                }
                half_v = MathF.Tan(fov * (MathF.PI / 180f) * 0.5f) * far_d;
                half_h = half_v * aspect;
            }
            Vector3 far_c = fin + fwd * far_d;
            Vector3 tl = far_c + up * half_v - right * half_h;
            Vector3 tr = far_c + up * half_v + right * half_h;
            Vector3 bl = far_c - up * half_v - right * half_h;
            Vector3 br = far_c - up * half_v + right * half_h;
            Draw3D_Line(fin, tl, 0.015f, col);
            Draw3D_Line(fin, tr, 0.015f, col);
            Draw3D_Line(fin, bl, 0.015f, col);
            Draw3D_Line(fin, br, 0.015f, col);
            Draw3D_Line(tl, tr, 0.015f, col);
            Draw3D_Line(tr, br, 0.015f, col);
            Draw3D_Line(br, bl, 0.015f, col);
            Draw3D_Line(bl, tl, 0.015f, col);
        }

        t.position = fin;
        t.rotation = cam_xf.rotation;
        Imp3D.Draw3D_Mesh(_cam_mesh, t);
    }

    protected override TBounds3 Bounds_Calc()
    {
        return _cam_mesh.Bounds_Get(Transform_GetForCamera());
    }

    public override Camera Camera_GetData()
    {
        A_CameraConfig cfg = Config_Get();
        TTransform3 cam_xf = Transform_GetForCamera();
        Quaternion rot = ImpMath.Euler_2_Quat(cam_xf.rotation);
        float fovy = (float)cfg.fov;
        if (fovy < 0.01f)
        {
            fovy = 0.01f;
        }
        Projection proj = Projection.Perspective;
        if (cfg.view_mode == ECameraViewMode.Orthographic)
        {
            proj = Projection.Orthographic;
        }
        return new Camera
        {
            Position = cam_xf.position,
            Rotation = rot,
            Fovy = fovy,
            NearPlane = 0.05,
            FarPlane = 1000,
            CullMask = Layer.All,
            Projection = proj,
        };
    }

    public override bool Camera_IsValid()
    {
        return true;
    }

    void Boom_ApplyConfig()
    {
        if (camera_boom == null)
        {
            return;
        }
        A_CameraConfig cfg = Config_Get();
        camera_boom.target_arm_length = (float)cfg.boom_distance;
        camera_boom.do_collision_test = cfg.boom_uses_collision;
        camera_boom.enable_camera_lag = cfg.boom_lag_position;
        camera_boom.camera_lag_speed = cfg.boom_lag_position_speed;
        camera_boom.enable_camera_rotation_lag = cfg.boom_lag_rotation;
        camera_boom.camera_rotation_lag_speed = cfg.boom_lag_rotation_speed;
    }

    Imp3D Pawn_Get()
    {
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            if (ImpPlayer.players[i].target_view == this)
            {
                return ImpPlayer.players[i].pawn;
            }
        }
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            if (ImpPlayer.players[i].pawn != null)
            {
                return ImpPlayer.players[i].pawn;
            }
        }
        return null;
    }
}
