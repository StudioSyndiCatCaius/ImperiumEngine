using System.Numerics;
using Engine.Core;
using Engine.Structs;
using Engine;
using Engine.Globals;
using R3D_cs;

namespace Engine.Comps._3D;

public enum ECameraTargetMode
{
    LookAt,
    Follow,
}

public class C3_Camera : Imp3D
{
    [ImpVar] public float fov = 60;
    [ImpVar] public float near_plane = 0.1f;
    [ImpVar] public float far_plane = 1000;
    [ImpVar] public bool is_orthographic = false;
    
    [ImpVar] public ECameraTargetMode target_mode = ECameraTargetMode.LookAt;
    [ImpVar] public Imp3D? cam_target;
    [ImpVar] public float target_interp_speed = 5;
    
    
    public C3_SpringArm camera_boom=new ();

    public C3_Camera()
    {
        Child_Add(camera_boom, true);
    }

    public override void OnInit()
    {
        base.OnInit();
        if (App.view_target == null) App.view_target = this;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (cam_target != null)
        {
            switch (target_mode)
            {
                case ECameraTargetMode.LookAt:
                    Vector3 look_rotation = GMath.V3_LookAt(global_transform.position, cam_target.global_transform.position);
                    Rotation_Set(GMath.V3_Interp(global_transform.rotation, look_rotation, dt, target_interp_speed));
                    break;
                case ECameraTargetMode.Follow:
                    Position_Set(GMath.V3_Interp(global_transform.position, cam_target.global_transform.position, dt, target_interp_speed));
                    break;
            }
        }
    }

    public override bool ViewTarget_Enabled() { return true; }
    public override Camera ViewTarget_GetData()
    {
        TTransform3 _t = global_transform;
        if (camera_boom != null && camera_boom.length != 0)
            _t = camera_boom.end_transform;

        return Camera_FromTransform(_t, fov, near_plane, far_plane, is_orthographic);
    }
}