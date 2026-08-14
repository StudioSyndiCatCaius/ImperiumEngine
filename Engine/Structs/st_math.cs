using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

public struct TTransform3 : I_Property
{
    [ImpVar] public Vector3 position;
    [ImpVar] public Vector3 rotation; //euler degrees, applied yaw/pitch/roll - see Imp3D
    [ImpVar] public Vector3 scale=Vector3.One;

    public TTransform3()
    {
        position = default;
        rotation = default;
        scale = Vector3.One;
    }

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty prop_ui)
    {
        prop_ui.Group_BuildNamed(nameof(position), nameof(rotation), nameof(scale));
    }
}

public struct TTransform2 : I_Property
{
    [ImpVar] public Vector2 position;
    [ImpVar] public double rotation; //degrees
    [ImpVar] public Vector2 scale=Vector2.One;

    public TTransform2()
    {
        position = default;
        rotation = 0;
        scale = Vector2.One;
    }

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty prop_ui)
    {
        prop_ui.Group_BuildNamed(nameof(position), nameof(rotation), nameof(scale));
    }
}
