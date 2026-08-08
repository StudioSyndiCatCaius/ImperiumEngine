using ImperiumEngine.Main;

namespace ImperiumEngine.Comps._3D;

public class C3_Camera : ImpComp3D
{
    [ImpVar][Export] public double fov;
    [ImpVar][Export] public double aspect_ratio;
    [ImpVar][Export] public double boom_distance;

    [ImpVar][Export] public ImpComp3D look_target;
    [ImpVar][Export] public double look_lerp=0.5;
    [ImpVar][Export] public double look_speed=1;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (look_target != null)
        {
            //if valid look target, update look at
        }
    }
}