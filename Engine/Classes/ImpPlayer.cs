using System.Numerics;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Objects.Config;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Classes;

//base player class
public class ImpPlayer
{
    // The single active player — used by the static input API so gameplay code can call
    // ImpPlayer.Action_IsDown("move") without needing a player reference.
    public static ImpPlayer? s_active;

    public ImpDevice[] devices = [];

    // Objects that receive dispatched input each frame. Only I_InputTarget implementors allowed.
    public List<I_InputTarget> input_targets = new();

    // Loaded from CFG_Input — maps action labels to their key bindings
    public TInputs inputs;

    public Camera3D camera = new()
    {
        Position   = new Vector3(0f, 8f, 12f),
        Target     = new Vector3(0f, 1f,  0f),
        Up         = Vector3.UnitY,
        FovY       = 60f,
        Projection = CameraProjection.Perspective
    };

    public ImpPlayer()
    {
        inputs = new CFG_Input().inputs;
    }

    public void OnUpdate(double delta)
    {
        // Dispatch each action's current axis to every registered input target
        foreach (var (action, _) in inputs.actions)
        {
            var axis = inputs.GetAxis(action);
            foreach (var target in input_targets)
                target.Input_Update(this, action, axis, (float)delta);
        }
    }

    // -----------------------------------------------------------------
    // Input — Actions (static API, operates on s_active)
    // -----------------------------------------------------------------

    public static bool Action_IsDown(TLabel action) =>
        s_active?.inputs.IsDown(action) ?? false;

    public static Vector3 Action_GetAxis(TLabel action) =>
        s_active?.inputs.GetAxis(action) ?? Vector3.Zero;

    public static bool Action_JustPressed(TLabel action) =>
        s_active?.inputs.JustPressed(action) ?? false;

    public static bool Action_JustReleased(TLabel action) =>
        s_active?.inputs.JustReleased(action) ?? false;

    // -----------------------------------------------------------------
    // Input — Raw keys (static API)
    // -----------------------------------------------------------------

    public static bool Key_IsDown(TKey key)      => key.IsDown();
    public static bool Key_JustPressed(TKey key)  => key.JustPressed();
    public static bool Key_JustReleased(TKey key) => key.JustReleased();

    // Mouse delta is continuous — not action-based, so exposed directly
    public static Vector2 Mouse_Delta() => Raylib.GetMouseDelta();
    public static float   Mouse_Scroll() => Raylib.GetMouseWheelMove();
}

public enum EDeviceType
{
    Keyboard, Mouse, Gamepad, Touch, Joystick,
}

//input devices linked to a player
public class ImpDevice
{
    public EDeviceType type;
}
