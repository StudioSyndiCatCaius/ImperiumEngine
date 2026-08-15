using ImperiumEngine;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Camera : Imp3D
{
    [ImpVar]  public double fov;
    [ImpVar]  public double aspect_ratio;
    [ImpVar]  public double boom_distance;

    [ImpVar]  public Imp3D look_target;
    [ImpVar]  public double look_lerp=0.5;
    [ImpVar]  public double look_speed=1;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (look_target != null)
        {
            //if valid look target, update look at
        }
    }
}