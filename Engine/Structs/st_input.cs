using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace ImperiumEngine.Structs;

// Runtime input state — maps action labels to their key bindings and queries them each frame
public class TInputs
{
    public Dictionary<TLabel, TInputActionConfig> actions = new();

    public bool IsDown(TLabel action) =>
        actions.TryGetValue(action, out var cfg) && cfg.IsDown();

    public bool JustPressed(TLabel action) =>
        actions.TryGetValue(action, out var cfg) && cfg.JustPressed();

    public bool JustReleased(TLabel action) =>
        actions.TryGetValue(action, out var cfg) && cfg.JustReleased();

    public Vector3 GetAxis(TLabel action) =>
        actions.TryGetValue(action, out var cfg) ? cfg.GetAxis() : Vector3.Zero;
}

// All the keys that contribute to one named action, each with their axis direction
public class TInputActionConfig
{
    public Dictionary<TKey, TInputKeyConfig> keys = new();

    public bool IsDown()      => keys.Keys.Any(k => k.IsDown());
    public bool JustPressed() => keys.Keys.Any(k => k.JustPressed());
    public bool JustReleased()=> keys.Keys.Any(k => k.JustReleased());

    public Vector3 GetAxis()
    {
        var result = Vector3.Zero;
        foreach (var (key, cfg) in keys)
            if (key.IsDown()) result += cfg.axis;
        return result;
    }
}

public enum EKeyDevice { Keyboard, MouseButton }

// A single physical key/button binding. Struct so it works as a dictionary key.
public struct TKey : IEquatable<TKey>
{
    public EKeyDevice device;
    public int        code;

    public static TKey Key(KeyboardKey k)  => new() { device = EKeyDevice.Keyboard,    code = (int)k };
    public static TKey Mouse(MouseButton b) => new() { device = EKeyDevice.MouseButton, code = (int)b };

    public bool IsDown() => device switch
    {
        EKeyDevice.Keyboard    => IsKeyDown((KeyboardKey)code),
        EKeyDevice.MouseButton => IsMouseButtonDown((MouseButton)code),
        _ => false
    };

    public bool JustPressed() => device switch
    {
        EKeyDevice.Keyboard    => IsKeyPressed((KeyboardKey)code),
        EKeyDevice.MouseButton => IsMouseButtonPressed((MouseButton)code),
        _ => false
    };

    public bool JustReleased() => device switch
    {
        EKeyDevice.Keyboard    => IsKeyReleased((KeyboardKey)code),
        EKeyDevice.MouseButton => IsMouseButtonReleased((MouseButton)code),
        _ => false
    };

    public bool Equals(TKey other) => device == other.device && code == other.code;
    public override bool Equals(object? obj) => obj is TKey k && Equals(k);
    public override int GetHashCode() => HashCode.Combine(device, code);
}

// Per-key contribution: how much and in which direction this key pushes the action axis
public class TInputKeyConfig
{
    public float   deadzone = 0.1f;
    public Vector3 axis     = Vector3.Zero;
}
