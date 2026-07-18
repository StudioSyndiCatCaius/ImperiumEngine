using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace ImperiumEngine.Objects._3D;

public class O3D_Camera : ImpComponent3D
{
    // The camera currently used for rendering — set by OnBegin so Program.cs can read it.
    public static O3D_Camera? active;

    public Camera3D raycamera = new()
    {
        Up         = Vector3.UnitY,
        FovY       = 60f,
        Projection = CameraProjection.Perspective
    };

    [ImpVar] public float fov         = 60f;
    [ImpVar] public float boom_length = 0f;

    public override void OnBegin() => active = this;
}


// Orbits around transform.Position. Right-mouse drag = orbit, scroll = zoom.
public class O3D_Camera_RotTest : O3D_Camera, I_InputTarget
{
    [ImpVar] public float sensitivity = 0.25f;
    [ImpVar] public float zoom_speed  = 1.5f;
    [ImpVar] public float pitch       = 20f;  // starting vertical angle (degrees, positive = looking down)

    float _yaw = 0f;

    public O3D_Camera_RotTest() => boom_length = 14f;

    public override void OnBegin()
    {
        base.OnBegin();
        ImpPlayer.s_active?.input_targets.Add(this);
        RefreshCamera();
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

        RefreshCamera();
    }

    public void Input_Update(ImpPlayer player, TLabel action, Vector3 axis, float delta) { }

    void RefreshCamera()
    {
        float yawRad   = _yaw  * (MathF.PI / 180f);
        float pitchRad = pitch * (MathF.PI / 180f);

        var dir = new Vector3(
            MathF.Sin(yawRad) * MathF.Cos(pitchRad),
            MathF.Sin(pitchRad),
            MathF.Cos(yawRad) * MathF.Cos(pitchRad));

        raycamera.Position   = transform.Position + dir * boom_length;
        raycamera.Target     = transform.Position;
        raycamera.Up         = Vector3.UnitY;
        raycamera.FovY       = fov;
        raycamera.Projection = CameraProjection.Perspective;
    }
}
