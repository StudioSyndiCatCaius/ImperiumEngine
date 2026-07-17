using System.Numerics;
using Raylib_cs;

namespace ImperiumEngine.Classes;

//base player class
public class ImpPlayer
{
    public ImpDevice[] devices = [];

    // All players share one camera — no splitscreen. Position: above & angled down toward the scene.
    public Camera3D camera = new()
    {
        Position   = new Vector3(0f, 8f, 12f),
        Target     = new Vector3(0f, 1f,  0f),
        Up         = Vector3.UnitY,
        FovY       = 60f,
        Projection = CameraProjection.Perspective
    };
}

public enum EDeviceType
{
    Keyboard, Mouse, Gamepad, Touch, Joystick,
}

//input devices linked to a player
public class ImpDevice
{

}
