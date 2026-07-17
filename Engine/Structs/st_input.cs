using System.Numerics;

namespace ImperiumEngine.Structs;

public class TInputActionConfig
{
    public Dictionary<TKey, TInputKeyConfig> keys;
}

public class TKey
{
    byte keycode;
}

public class TInputKeyConfig
{
    public float deadzone;
    public Vector3 axis;
}