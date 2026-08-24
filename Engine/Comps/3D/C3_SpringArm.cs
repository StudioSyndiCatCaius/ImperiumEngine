using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_SpringArm : Imp3D
{
    public const string SocketName = "SpringEndpoint";

    const float KindaSmallNumber = 1e-4f;
    const float SmallNumber = 1e-8f;
    const float PhysMaxDelta = 1f / 15f;

    [ImpVar][Category("Camera")] public float target_arm_length = 3f;
    [ImpVar][Category("Camera")] public Vector3 socket_offset;
    [ImpVar][Category("Camera")] public Vector3 target_offset;

    [ImpVar][Category("CameraCollision")] public bool do_collision_test = true;
    [ImpVar][Category("CameraCollision")] public float probe_size = 0.12f;
    [ImpVar][Category("CameraCollision")] public ECollisionChannel probe_channel = ECollisionChannel.World;

    [ImpVar][Category("CameraSettings")] public bool use_pawn_control_rotation;
    [ImpVar][Category("CameraSettings")] public bool inherit_pitch = true;
    [ImpVar][Category("CameraSettings")] public bool inherit_yaw = true;
    [ImpVar][Category("CameraSettings")] public bool inherit_roll = true;

    [ImpVar][Category("Lag")] public bool enable_camera_lag;
    [ImpVar][Category("Lag")] public bool enable_camera_rotation_lag;
    [ImpVar][Category("Lag")] public float camera_lag_speed = 10f;
    [ImpVar][Category("Lag")] public float camera_rotation_lag_speed = 10f;
    [ImpVar][Category("Lag")] public float camera_lag_max_distance;
    [ImpVar][Category("Lag")] public bool draw_debug_lag_markers;
    [ImpVar(Advanced = true)][Category("Lag")] public bool use_camera_lag_substepping = true;
    [ImpVar(Advanced = true)][Category("Lag")] public float camera_lag_max_time_step = 1f / 60f;
    [ImpVar(Advanced = true)][Category("Lag")] public bool clamp_to_max_physics_delta_time;

    public bool is_camera_fixed;
    public Vector3 unfixed_camera_position;

    Vector3 _relative_socket_location;
    Quaternion _relative_socket_rotation = Quaternion.Identity;
    TTransform3 _socket_world;

    Vector3 _previous_desired_loc;
    Vector3 _previous_arm_origin;
    Quaternion _previous_desired_rot = Quaternion.Identity;
    bool _lag_inited;
    bool _runtime_updated;
    bool _ticked_by_parent;

    Vector3 _debug_arm_origin;
    Vector3 _debug_lagged_origin;
    bool _debug_lag_clamped;

    public Vector3 GetUnfixedCameraPosition()
    {
        return unfixed_camera_position;
    }

    public bool IsCollisionFixApplied()
    {
        return is_camera_fixed;
    }

    public TTransform3 Socket_WorldTransform()
    {
        if (!_runtime_updated)
        {
            UpdateDesiredArmLocation(do_collision_test, false, false, 0f);
        }
        return _socket_world;
    }

    public Vector3 Socket_LocalLocation()
    {
        if (!_runtime_updated)
        {
            UpdateDesiredArmLocation(do_collision_test, false, false, 0f);
        }
        return _relative_socket_location;
    }

    public Quaternion Socket_LocalRotation()
    {
        if (!_runtime_updated)
        {
            UpdateDesiredArmLocation(do_collision_test, false, false, 0f);
        }
        return _relative_socket_rotation;
    }

    public Vector3 GetDesiredRotation()
    {
        return Rotation_Get(true);
    }

    public Vector3 GetTargetRotation()
    {
        Vector3 desired = GetDesiredRotation();

        if (use_pawn_control_rotation)
        {
            Vector3 view = ViewRotation_Get();
            if (inherit_pitch)
            {
                desired.X = view.X;
            }
            if (inherit_yaw)
            {
                desired.Y = view.Y;
            }
            if (inherit_roll)
            {
                desired.Z = view.Z;
            }
        }

        if (!inherit_pitch || !inherit_yaw || !inherit_roll)
        {
            Vector3 local = Rotation_Get(false);
            if (!inherit_pitch)
            {
                desired.X = local.X;
            }
            if (!inherit_yaw)
            {
                desired.Y = local.Y;
            }
            if (!inherit_roll)
            {
                desired.Z = local.Z;
            }
        }

        return desired;
    }

    public void ResetLag()
    {
        _runtime_updated = false;
        _lag_inited = false;
        UpdateDesiredArmLocation(false, false, false, 0f);
    }

    public void Arm_Update(float dt)
    {
        if (clamp_to_max_physics_delta_time && dt > PhysMaxDelta)
        {
            dt = PhysMaxDelta;
        }
        UpdateDesiredArmLocation(do_collision_test, enable_camera_lag, enable_camera_rotation_lag, dt);
        _runtime_updated = true;
        _ticked_by_parent = true;
    }

    public override void OnBegin()
    {
        base.OnBegin();
        _runtime_updated = false;
        _ticked_by_parent = false;
        _lag_inited = false;
        UpdateDesiredArmLocation(false, false, false, 0f);
    }

    public override void OnEnd()
    {
        base.OnEnd();
        _runtime_updated = false;
        _ticked_by_parent = false;
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!_ticked_by_parent)
        {
            Arm_Update((float)dt);
        }
        _ticked_by_parent = false;
    }

    public override void OnDraw3D(double dt, EDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (!flags.HasFlag(EDrawFlags.Editor))
        {
            return;
        }

        TTransform3 origin = Transform_Get(true);
        TTransform3 socket = Socket_WorldTransform();
        if (!(parent is C3_Camera))
        {
            bool selected = flags.HasFlag(EDrawFlags.Selected);
            Color col = Color.White;
            if (selected)
            {
                col = ImpGizmo.ColSelect;
            }
            float thick = 0.02f;
            if (selected)
            {
                thick = 0.03f;
            }
            Draw3D_Line(origin.position, socket.position, thick, col);
        }

        if (draw_debug_lag_markers && enable_camera_lag)
        {
            TTransform3 mark = new TTransform3();
            mark.position = _debug_arm_origin;
            Draw3D_Sphere(mark, 0.08f, 12, Color.Green);
            mark.position = _debug_lagged_origin;
            Draw3D_Sphere(mark, 0.08f, 12, Color.Yellow);
            Color lag_col = Color.Green;
            if (_debug_lag_clamped)
            {
                lag_col = Color.Red;
            }
            Draw3D_Line(_debug_arm_origin, _debug_lagged_origin, 0.02f, lag_col);
        }
    }

    protected override TBounds3 Bounds_Calc()
    {
        TTransform3 origin = Transform_Get(true);
        TTransform3 socket = Socket_WorldTransform();
        Vector3 min = Vector3.Min(origin.position, socket.position);
        Vector3 max = Vector3.Max(origin.position, socket.position);
        Vector3 size = max - min;
        if (size.X < 0.15f)
        {
            size.X = 0.15f;
        }
        if (size.Y < 0.15f)
        {
            size.Y = 0.15f;
        }
        if (size.Z < 0.15f)
        {
            size.Z = 0.15f;
        }
        return new TBounds3
        {
            center = (min + max) * 0.5f,
            size = size,
            rotation = Vector3.Zero,
        };
    }

    protected virtual void UpdateDesiredArmLocation(bool do_trace, bool do_location_lag, bool do_rotation_lag, float dt)
    {
        Vector3 desired_rot_e = GetTargetRotation();
        Quaternion desired_rot = ImpMath.Euler_2_Quat(desired_rot_e);

        if (!_lag_inited)
        {
            _previous_desired_rot = desired_rot;
            TTransform3 w0 = Transform_Get(true);
            _previous_arm_origin = w0.position + target_offset;
            _previous_desired_loc = _previous_arm_origin;
            _lag_inited = true;
        }

        if (do_rotation_lag)
        {
            if (use_camera_lag_substepping && dt > camera_lag_max_time_step && camera_rotation_lag_speed > 0f)
            {
                Quaternion from = _previous_desired_rot;
                Quaternion to = desired_rot;
                float remaining = dt;
                float inv_dt = 1f / dt;
                while (remaining > KindaSmallNumber)
                {
                    float step = camera_lag_max_time_step;
                    if (step > remaining)
                    {
                        step = remaining;
                    }
                    float alpha = step * inv_dt;
                    if (alpha > 1f)
                    {
                        alpha = 1f;
                    }
                    Quaternion lerp_target = Quaternion.Slerp(from, to, alpha);
                    desired_rot = QInterpTo(_previous_desired_rot, lerp_target, step, camera_rotation_lag_speed);
                    _previous_desired_rot = desired_rot;
                    remaining -= step;
                }
            }
            else
            {
                desired_rot = QInterpTo(_previous_desired_rot, desired_rot, dt, camera_rotation_lag_speed);
            }
        }
        _previous_desired_rot = desired_rot;
        desired_rot_e = ImpMath.Quat_2_Euler(desired_rot);

        TTransform3 world = Transform_Get(true);
        Vector3 arm_origin = world.position + target_offset;
        Vector3 desired_loc = arm_origin;
        bool lag_clamped = false;

        if (do_location_lag)
        {
            if (use_camera_lag_substepping && dt > camera_lag_max_time_step && camera_lag_speed > 0f)
            {
                Vector3 movement_step = (arm_origin - _previous_arm_origin) * (1f / dt);
                Vector3 lerp_target = _previous_arm_origin;
                float remaining = dt;
                while (remaining > KindaSmallNumber)
                {
                    float step = camera_lag_max_time_step;
                    if (step > remaining)
                    {
                        step = remaining;
                    }
                    lerp_target += movement_step * step;
                    remaining -= step;
                    desired_loc = VInterpTo(_previous_desired_loc, lerp_target, step, camera_lag_speed);
                    _previous_desired_loc = desired_loc;
                }
            }
            else
            {
                desired_loc = VInterpTo(_previous_desired_loc, desired_loc, dt, camera_lag_speed);
            }

            if (camera_lag_max_distance > 0f)
            {
                Vector3 from_origin = desired_loc - arm_origin;
                float max = camera_lag_max_distance;
                if (from_origin.LengthSquared() > max * max)
                {
                    float len = from_origin.Length();
                    if (len > SmallNumber)
                    {
                        from_origin = from_origin * (max / len);
                    }
                    desired_loc = arm_origin + from_origin;
                    lag_clamped = true;
                }
            }
        }

        _previous_arm_origin = arm_origin;
        _previous_desired_loc = desired_loc;
        _debug_arm_origin = arm_origin;
        _debug_lagged_origin = desired_loc;
        _debug_lag_clamped = lag_clamped;

        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, desired_rot);
        desired_loc = desired_loc - forward * target_arm_length;
        desired_loc = desired_loc + Vector3.Transform(socket_offset, desired_rot);

        Vector3 result_loc;
        if (do_trace && MathF.Abs(target_arm_length) > SmallNumber)
        {
            is_camera_fixed = true;
            unfixed_camera_position = desired_loc;

            Vector3 hit_loc = desired_loc;
            bool hit_something = Probe_Trace(arm_origin, desired_loc, out hit_loc);
            result_loc = BlendLocations(desired_loc, hit_loc, hit_something, dt);
            if (result_loc == desired_loc)
            {
                is_camera_fixed = false;
            }
        }
        else
        {
            result_loc = desired_loc;
            is_camera_fixed = false;
            unfixed_camera_position = result_loc;
        }

        TTransform3 socket_world = new TTransform3
        {
            position = result_loc,
            rotation = desired_rot_e,
            scale = world.scale,
        };
        TTransform3 rel = LocalFromComponent(world, socket_world);
        _relative_socket_location = rel.position;
        _relative_socket_rotation = ImpMath.Euler_2_Quat(rel.rotation);
        _socket_world = socket_world;
    }

    protected virtual Vector3 BlendLocations(Vector3 desired_arm_location, Vector3 trace_hit_location, bool hit_something, float dt)
    {
        if (hit_something)
        {
            return trace_hit_location;
        }
        return desired_arm_location;
    }

    bool Probe_Trace(Vector3 arm_origin, Vector3 desired_loc, out Vector3 hit_loc)
    {
        hit_loc = desired_loc;
        ImpPhys phys = null;
        if (game_owner != null)
        {
            phys = game_owner.phys;
        }
        if (phys == null)
        {
            return false;
        }

        Imp3D pawn = Pawn_ForIgnore();
        Imp3D self = this;
        ImpComp owner = parent;
        TTraceResult3D hit = phys.Trace_Line(arm_origin, desired_loc, probe_channel, c =>
        {
            if (c == null)
            {
                return true;
            }
            if (c == self || c.IsDescendantOf(self))
            {
                return false;
            }
            if (owner is C3_Camera || owner is C3_Character)
            {
                if (c == owner || c.IsDescendantOf(owner))
                {
                    return false;
                }
            }
            else if (c == owner)
            {
                return false;
            }
            if (pawn != null && (c == pawn || c.IsDescendantOf(pawn)))
            {
                return false;
            }
            return true;
        });

        if (!hit.hit)
        {
            return false;
        }

        Vector3 delta = desired_loc - arm_origin;
        float delta_len = delta.Length();
        Vector3 dir;
        if (delta_len > SmallNumber)
        {
            dir = delta / delta_len;
        }
        else
        {
            dir = Vector3.Zero;
        }
        float skin = probe_size;
        if (skin < 0.001f)
        {
            skin = 0.001f;
        }
        hit_loc = hit.hit_position - dir * skin;
        Vector3 from_origin = hit_loc - arm_origin;
        if (Vector3.Dot(from_origin, dir) < 0f)
        {
            hit_loc = arm_origin;
        }
        return true;
    }

    Imp3D Pawn_ForIgnore()
    {
        if (parent is C3_Camera)
        {
            for (int i = 0; i < ImpPlayer.players.Count; i++)
            {
                if (ImpPlayer.players[i].pawn != null)
                {
                    return ImpPlayer.players[i].pawn;
                }
            }
        }
        for (ImpComp n = parent; n != null; n = n.parent)
        {
            if (n is C3_Character ch)
            {
                return ch;
            }
        }
        return null;
    }

    Vector3 ViewRotation_Get()
    {
        if (parent is C3_Camera cam)
        {
            return cam.Rotation_Get(true);
        }
        if (ImpApp.view_target != null && ImpApp.view_target.Camera_IsValid())
        {
            return ImpApp.view_target.Rotation_Get(true);
        }
        return Rotation_Get(true);
    }

    static TTransform3 LocalFromComponent(TTransform3 parent, TTransform3 world)
    {
        Quaternion pq = ImpMath.Euler_2_Quat(parent.rotation);
        Vector3 inv_s = Vector3.Zero;
        if (parent.scale.X != 0)
        {
            inv_s.X = 1f / parent.scale.X;
        }
        if (parent.scale.Y != 0)
        {
            inv_s.Y = 1f / parent.scale.Y;
        }
        if (parent.scale.Z != 0)
        {
            inv_s.Z = 1f / parent.scale.Z;
        }
        return new TTransform3
        {
            position = Vector3.Transform(world.position - parent.position, Quaternion.Inverse(pq)) * inv_s,
            rotation = ImpMath.Quat_2_Euler(Quaternion.Inverse(pq) * ImpMath.Euler_2_Quat(world.rotation)),
            scale = world.scale * inv_s
        };
    }

    static Vector3 VInterpTo(Vector3 current, Vector3 target, float dt, float speed)
    {
        if (speed <= 0f)
        {
            return target;
        }
        Vector3 dist = target - current;
        if (dist.LengthSquared() < SmallNumber)
        {
            return target;
        }
        float alpha = dt * speed;
        if (alpha > 1f)
        {
            alpha = 1f;
        }
        if (alpha < 0f)
        {
            alpha = 0f;
        }
        return current + dist * alpha;
    }

    static Quaternion QInterpTo(Quaternion current, Quaternion target, float dt, float speed)
    {
        if (speed <= 0f)
        {
            return target;
        }
        float alpha = dt * speed;
        if (alpha >= 1f)
        {
            return target;
        }
        if (alpha <= 0f)
        {
            return current;
        }
        return Quaternion.Slerp(current, target, alpha);
    }
}
