using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

public class TInputAction : I_Property
{
    [ImpVar] public string name;
    [ImpVar] public Dictionary<EInputKey, TInputKey> keys = new();
    
}

public class TInputKey
{
    [ImpVar] public List<EInputKey> prereq_keys = new(); // keys that must be Down before this main key can be considered pressed
    [ImpVar] public float deadzone;
    [ImpVar] public Vector3 axis_scale;
}