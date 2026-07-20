using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace ImperiumEngine.Objects._3D;

public struct TSpringArmLagConfig
{
    [ImpVar] public bool enabled;
    [ImpVar] public float scale=1.0f;                // smoothing speed; higher = snappier, 0 = no lag
    [ImpVar] public Vector3 weight=Vector3.One;  // per-axis multiplier on the smoothing speed

    // runtime smoothing state — deliberately not [ImpVar], so it is neither serialized nor shown
    Vector3 _current;
    bool    _primed;

    public TSpringArmLagConfig()
    {
        enabled = false;
        scale = 0;
    }

    // Frame-rate-independent exponential smoothing toward 'target'.
    // The first call — and any call while disabled — snaps and (re)primes the state.
    public Vector3 Smooth(Vector3 target, double delta)
    {
        if (!enabled || scale <= 0f || !_primed)
        {
            _current = target;
            _primed  = true;
            return target;
        }

        _current = new Vector3(
            Damp(_current.X, target.X, weight.X, delta),
            Damp(_current.Y, target.Y, weight.Y, delta),
            Damp(_current.Z, target.Z, weight.Z, delta));
        return _current;
    }

    readonly float Damp(float from, float to, float w, double delta)
    {
        float t = 1f - MathF.Exp(-scale * w * (float)delta);
        return from + (to - from) * t;
    }
}

public class C3_Camera : ImpComponent3D
{
    // The camera currently used for rendering — set by OnBegin so Program.cs can read it.
    public static C3_Camera? active;

    public Camera3D raycamera = new()
    {
        Up         = Vector3.UnitY,
        FovY       = 60f,
        Projection = CameraProjection.Perspective
    };
    
    [ImpVar] public float fov         = 60f;
    [ImpVar] public float boom_length = 0f;
    
    [ImpVar] public TSpringArmLagConfig boom_lag_position;
    [ImpVar] public TSpringArmLagConfig boom_lag_rotation;

    [ImpVar] public bool is_orthographic;
    
    //TO-DO: allow cameras to capture to a render texture
    [ImpVar] public bool capture_to_render_texture;
    //if empty, captures everything in view. If this list has items, only captures those and their children.
    [ImpVar] public LinkedList<ImpComponent> capture_list;
    
    
    public override void OnBegin() => active = this;
    

    // Builds raycamera from a look-at pivot and a unit boom direction (pointing from the
    // pivot toward the camera). Position lag smooths the pivot; rotation lag smooths the
    // boom direction — together they make the camera trail its target instead of snapping.
    protected void UpdateBoom(Vector3 pivot, Vector3 boom_dir, double delta)
    {
        var lagged_pivot = boom_lag_position.Smooth(pivot, delta);

        var lagged_dir = boom_lag_rotation.Smooth(boom_dir, delta);
        lagged_dir = lagged_dir.LengthSquared() > 1e-6f
            ? Vector3.Normalize(lagged_dir)
            : boom_dir;

        raycamera.Position   = lagged_pivot + lagged_dir * boom_length;
        raycamera.Target     = lagged_pivot;
        raycamera.Up         = Vector3.UnitY;
        raycamera.FovY       = fov;
        raycamera.Projection = CameraProjection.Perspective;
    }
}


// Orbits around transform.Position. Right-mouse drag = orbit, scroll = zoom.
public class C3_Camera_RotTest : C3_Camera, I_InputTarget
{
    [ImpVar] public float sensitivity = 0.25f;
    [ImpVar] public float zoom_speed  = 1.5f;
    [ImpVar] public float pitch       = 20f;  // starting vertical angle (degrees, positive = looking down)

    float _yaw = 0f;

    public C3_Camera_RotTest() => boom_length = 14f;

    public override void OnBegin()
    {
        base.OnBegin();
        ImpPlayer.s_active?.input_targets.Add(this);
        RefreshCamera(0);   // delta 0 primes the lag state so the first frame snaps into place
    }

    public override void OnUpdate(double delta)
    {
        if (IsMouseButtonDown(MouseButton.Right))
        {
            var d = GetMouseDelta();
            _yaw += d.X * sensitivity;
            pitch  = Math.Clamp(pitch + d.Y * sensitivity, -89f, 89f);
        }

        float scroll = GetMouseWheelMove();
        if (scroll != 0f)
            boom_length = MathF.Max(1f, boom_length - scroll * zoom_speed);

        RefreshCamera(delta);
    }

    public void Input_Update(ImpPlayer player, TLabel action, Vector3 axis, float delta) { }

    void RefreshCamera(double delta)
    {
        float yawRad   = _yaw  * (MathF.PI / 180f);
        float pitchRad = pitch * (MathF.PI / 180f);

        var dir = new Vector3(
            MathF.Sin(yawRad) * MathF.Cos(pitchRad),
            MathF.Sin(pitchRad),
            MathF.Cos(yawRad) * MathF.Cos(pitchRad));

        UpdateBoom(WorldPosition, dir, delta);
    }
}
