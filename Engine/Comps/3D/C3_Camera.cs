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

    static A_Mesh _cam_mesh = A_Mesh.UTIL_CAMERA;

    Vector3 _aim;
    bool _start_applied;

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
        TTransform3 w = Transform_Get(true);
        A_CameraConfig cfg = Config_Get();
        float boom = (float)cfg.boom_distance;
        if (boom > 0.001f)
        {
            Quaternion rot = ImpMath.Euler_2_Quat(w.rotation);
            Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rot);
            Vector3 pivot = w.position;
            if (cfg.boom_uses_collision)
            {
                Imp3D pawn = null;
                for (int i = 0; i < ImpPlayer.players.Count; i++)
                {
                    if (ImpPlayer.players[i].pawn != null)
                    {
                        pawn = ImpPlayer.players[i].pawn;
                        break;
                    }
                }
                float skin = 0.15f;
                float start_d = 0.05f;
                if (pawn is C3_Collider col)
                {
                    float rad = col.extents.X;
                    if (col.extents.Z > rad)
                    {
                        rad = col.extents.Z;
                    }
                    start_d = rad + 0.05f;
                }
                if (start_d >= boom)
                {
                    start_d = boom * 0.25f;
                }
                Vector3 ray_start = pivot - forward * start_d;
                Vector3 ray_end = pivot - forward * boom;
                ImpPhys phys = null;
                if (game_owner != null)
                {
                    phys = game_owner.phys;
                }
                TTraceResult3D hit;
                if (phys != null)
                {
                    hit = phys.Trace_Line(ray_start, ray_end, ECollisionChannel.World, c =>
                    {
                        if (c == null)
                        {
                            return true;
                        }
                        if (c == this || c == pawn)
                        {
                            return false;
                        }
                        if (pawn != null && c.IsDescendantOf(pawn))
                        {
                            return false;
                        }
                        return true;
                    });
                }
                else
                {
                    hit = default;
                }
                if (hit.hit)
                {
                    float dist = Vector3.Distance(pivot, hit.hit_position) - skin;
                    if (dist < 0.05f)
                    {
                        dist = 0.05f;
                    }
                    if (dist < boom)
                    {
                        boom = dist;
                    }
                }
            }
            w.position = pivot - forward * boom;
        }
        return w;
    }

    public override void OnBegin()
    {
        base.OnBegin();
        _aim = transform.rotation;
        _start_applied = false;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        A_CameraConfig cfg = Config_Get();
        bool is_view = ImpApp.view_target == this;
        if (is_view && !_start_applied)
        {
            _aim = cfg.starting_rotation;
            Rotation_Set(_aim, false);
            _start_applied = true;
        }
        if (!is_view)
        {
            _start_applied = false;
        }

        if (is_view)
        {
            Imp3D pawn = null;
            for (int i = 0; i < ImpPlayer.players.Count; i++)
            {
                if (ImpPlayer.players[i].pawn != null)
                {
                    pawn = ImpPlayer.players[i].pawn;
                    break;
                }
            }
            if (pawn != null)
            {
                Vector3 pos = pawn.Position_Get(true);
                if (pawn is C3_Collider col)
                {
                    col.Shape_Local(out _, out Vector3 center);
                    TTransform3 pw = pawn.Transform_Get(true);
                    Quaternion q = ImpMath.Euler_2_Quat(pw.rotation);
                    pos = pos + Vector3.Transform(center * pw.scale, q);
                }
                Position_Set(pos, true);
            }
        }

        if (look_target != null)
        {
            Vector3 from = Position_Get(true);
            Vector3 to = look_target.Position_Get(true);
            Vector3 d = to - from;
            if (d.LengthSquared() > 1e-8f)
            {
                float yaw = MathF.Atan2(d.X, -d.Z) * (180f / MathF.PI);
                float horiz = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
                float pitch = MathF.Atan2(d.Y, horiz) * (180f / MathF.PI);
                if (pitch > 89.9f)
                {
                    pitch = 89.9f;
                }
                if (pitch < -89.9f)
                {
                    pitch = -89.9f;
                }
                Vector3 world_aim = new(pitch, yaw, 0f);
                if (parent is Imp3D p3)
                {
                    Vector3 parent_r = p3.Rotation_Get(true);
                    Quaternion local_q = Quaternion.Inverse(ImpMath.Euler_2_Quat(parent_r)) * ImpMath.Euler_2_Quat(world_aim);
                    _aim = ImpMath.Quat_2_Euler(local_q);
                }
                else
                {
                    _aim = world_aim;
                }
            }
        }

        if (!is_view && look_target == null && input_owner == null)
        {
            return;
        }

        float k = (float)cfg.look_lerp;
        if (k <= 0.0001f)
        {
            Rotation_Set(_aim, false);
            return;
        }
        float t = 1f - MathF.Exp(-(float)dt * k * 12f);
        Quaternion cur = ImpMath.Euler_2_Quat(Rotation_Get(false));
        Quaternion want = ImpMath.Euler_2_Quat(_aim);
        Rotation_Set(ImpMath.Quat_2_Euler(Quaternion.Slerp(cur, want, t)), false);
    }

    public override void Input_Update(ImpPlayer player, TLabel iaction, double dt, Vector3 axis)
    {
        base.Input_Update(player, iaction, dt, axis);
        A_CameraConfig cfg = Config_Get();
        if (iaction == "_Rotate")
        {
            if (look_target != null)
            {
                return;
            }
            if (!cfg.enable_rotate_H && !cfg.enable_rotate_V)
            {
                return;
            }
            float scale = (float)cfg.look_speed;
            if (MathF.Abs(axis.X) <= 2f && MathF.Abs(axis.Y) <= 2f)
            {
                scale *= 180f * (float)dt;
            }
            if (cfg.enable_rotate_H)
            {
                _aim.Y -= axis.Y * scale;
            }
            if (cfg.enable_rotate_V)
            {
                _aim.X -= axis.X * scale;
            }
            if (_aim.X > 89.9f)
            {
                _aim.X = 89.9f;
            }
            if (_aim.X < -89.9f)
            {
                _aim.X = -89.9f;
            }
            return;
        }
        if (iaction != "_Move")
        {
            return;
        }
        if (!cfg.enable_move)
        {
            return;
        }
        Imp3D pawn = player.pawn;
        if (pawn == null)
        {
            return;
        }
        Vector3 local = new(axis.Z, axis.Y, -axis.X);
        Vector3 yaw_only = new(0f, Rotation_Get(true).Y, 0f);
        pawn.Phys_MoveByRot(local, 1, yaw_only);
    }

    public override void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        base.Input_Pressed(player, iaction, axis);
        if (iaction != "_Jump")
        {
            return;
        }
        if (!Config_Get().enable_move)
        {
            return;
        }
        Imp3D pawn = player.pawn;
        if (pawn == null)
        {
            return;
        }
        if (!pawn.is_grounded)
        {
            return;
        }
        A_MoveMode mode = pawn.move_mode;
        if (mode == null)
        {
            mode = A_MoveMode.DEFAULT;
        }
        pawn.Phys_Launch(Vector3.UnitY, mode.jump_speed, false, true);
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
}
