using ImperiumEngine;

namespace ImperiumEngine.Comps._3D;

public class C3_SpringArm : ImpComp3D
{
    [ImpVar]  public double distance;

    [ImpVar]  public bool lag_location_enabled = false;
    [ImpVar]  public float lag_location_amount = 10.0f;
    [ImpVar]  public bool lag_rotation_enabled = false;
    [ImpVar]  public float lag_rotation_amount = 10.0f;
}