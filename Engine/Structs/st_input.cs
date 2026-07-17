using System.Numerics;
using ImperiumEngine.Interfaces;
using Silk.NET.Input;

namespace ImperiumCore.Structs;

// Which physical device a TKey refers to.
public enum EKeyDevice : byte
{
    None          = 0,
    Keyboard      = 1,
    Mouse         = 2,
    GamepadButton = 3,
    GamepadAxis   = 4,
}

// Analog sources on a gamepad, used when a TKey's Device is GamepadAxis.
public enum EGamepadAxis : byte
{
    LeftStick    = 0,
    RightStick   = 1,
    LeftTrigger  = 2,
    RightTrigger = 3,
}

// wrapper for keyboard/gamepad/any device keys.
// The device type and a device-specific code are packed into a single uint so
// one value can describe any button/axis on any device.
public struct TKey : IEquatable<TKey> , I_PropertyType
{
    private uint keycode; // [31..24] = EKeyDevice, [23..0] = device code

    public TKey(EKeyDevice device, int code)
        => keycode = ((uint)device << 24) | ((uint)code & 0x00FF_FFFF);

    public readonly EKeyDevice Device => (EKeyDevice)(keycode >> 24);
    public readonly int        Code   => (int)(keycode & 0x00FF_FFFF);

    public static TKey Keyboard(Key key)         => new(EKeyDevice.Keyboard, (int)key);
    public static TKey Mouse(MouseButton button) => new(EKeyDevice.Mouse, (int)button);
    public static TKey Button(ButtonName button) => new(EKeyDevice.GamepadButton, (int)button);
    public static TKey Axis(EGamepadAxis axis)   => new(EKeyDevice.GamepadAxis, (int)axis);

    // Convenience: let Silk.NET key/button enums be used directly as a TKey.
    public static implicit operator TKey(Key key)         => Keyboard(key);
    public static implicit operator TKey(MouseButton btn) => Mouse(btn);
    public static implicit operator TKey(ButtonName btn)  => Button(btn);

    public readonly bool Equals(TKey other)      => keycode == other.keycode;
    public override readonly bool Equals(object? obj) => obj is TKey k && Equals(k);
    public override readonly int  GetHashCode()   => (int)keycode;

    public static bool operator ==(TKey a, TKey b) =>  a.Equals(b);
    public static bool operator !=(TKey a, TKey b) => !a.Equals(b);
}


// A single key bound into a TInput action. While the key is active it
// contributes Axis to the action's combined axis.
public struct TInputKey
{
    public TKey    Key;
    public Vector3 Axis;     // contribution to the action axis when the key is active
    public double  Deadzone; // analog threshold below which the key counts as inactive
}

// A named input action made of one or more key bindings. Lets several physical
// keys (e.g. WASD + left stick) drive a single logical action.
public struct TInput
{
    public List<TInputKey> Keys;
}
