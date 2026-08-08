using ImperiumEngine.Main;

namespace ImperiumEngine.Comps._3D;

public class C3_SpringArm : ImpComp3D
{
    [ImpVar][Export] public double distance;

    [ImpVar][Export] public bool lag_location_enabled = false;
    [ImpVar][Export] public float lag_location_amount = 10.0f;
    [ImpVar][Export] public bool lag_rotation_enabled = false;
    [ImpVar][Export] public float lag_rotation_amount = 10.0f;
}