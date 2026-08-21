using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Camera : Imp3D
{
    [ImpVar]  public double fov=60;
    [ImpVar]  public double aspect_ratio;
    [ImpVar]  public double boom_distance;

    [ImpVar]  public Imp3D look_target;
    [ImpVar]  public double look_lerp=0.5;
    [ImpVar]  public double look_speed=1;
    
    static A_Mesh _cam_mesh=A_Mesh.UTIL_CAMERA;
    
    public TTransform3 Transform_GetForCamera()
    {
        var w = Transform_Get(true);
        Quaternion rot = ImpMath.Euler_2_Quat(w.rotation);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rot);
        w.position=w.position - forward * (float)boom_distance;
        return w;
    }
    

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (look_target != null)
        {
            //if valid look target, update look at
        }
    }

    public override void OnDraw3D(double dt, EDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (!flags.HasFlag(EDrawFlags.Editor))
        {
            return;
        }

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
            float aspect = (float)aspect_ratio;
            if (aspect < 0.01f)
            {
                aspect = 16f / 9f;
            }
            float far_d = 2.5f;
            float half_v = MathF.Tan((float)fov * (MathF.PI / 180f) * 0.5f) * far_d;
            float half_h = half_v * aspect;
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
        Imp3D.Draw3D_Mesh(_cam_mesh, t);
    }

    protected override TBounds3 Bounds_Calc()
    {
        return _cam_mesh.Bounds_Get(Transform_GetForCamera());
    }

    public override Camera Camera_GetData()
    {
        var w = Transform_Get(true);
        Quaternion rot = ImpMath.Euler_2_Quat(w.rotation);

        Camera _cam = new Camera()
        {
            Position = Transform_GetForCamera().position,
            Rotation = rot,
            Fovy = fov,
            NearPlane = 0.05,
            FarPlane = 1000,
            CullMask = Layer.All,
            Projection = Projection.Perspective,
        };
        return _cam;
    }

    public override bool Camera_IsValid() { return true; }
}