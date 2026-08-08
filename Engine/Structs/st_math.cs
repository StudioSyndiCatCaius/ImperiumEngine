using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

public struct TTransform3 : I_Property
{
    public Vector3 position;
    public Vector3 rotation; //euler degrees, applied yaw/pitch/roll - see Imp3D
    public Vector3 scale=Vector3.One;

    public TTransform3()
    {
        position = default;
        rotation = default;
    }

    // Taking the row over rather than letting the inspector reflect it. Reflection would
    // hand back the three vectors in whatever order it found them and expand each into its
    // own collapsible group; a transform is the one thing in the inspector whose shape
    // everybody already knows, so it is spelled out here instead.
    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty prop_ui)
    {
        prop_ui.Group_BuildNamed(nameof(position), nameof(rotation), nameof(scale));
    }
}

public struct TTransform2 : I_Property
{
    public Vector2 position;
    public double rotation; //degrees
    public Vector2 scale=Vector2.One;

    public TTransform2()
    {
        position = default;
        rotation = 0;
    }

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty prop_ui)
    {
        prop_ui.Group_BuildNamed(nameof(position), nameof(rotation), nameof(scale));
    }
}
