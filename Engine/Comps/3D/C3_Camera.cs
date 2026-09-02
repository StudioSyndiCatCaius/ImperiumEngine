using System.Numerics;
using Engine.Core;
using Engine.Structs;
using Engine;
using Engine.Globals;
using R3D_cs;

namespace Engine.Comps._3D;

public class C3_Camera : Imp3D
{
    [ImpVar] public float fov = 60;
    [ImpVar] public float near_plane = 0.1f;
    [ImpVar] public float far_plane = 1000;
    [ImpVar] public bool is_orthographic = false;
    
    [ImpVar] public Imp3D? look_target;
    [ImpVar] public float look_speed = 5;
    
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
        if (look_target != null)
        {
            Vector3 look_rotation = GMath.V3_LookAt(global_transform.position, look_target.global_transform.position);
            Rotation_Set(GMath.V3_Interp(global_transform.rotation, look_rotation, dt, look_speed));
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