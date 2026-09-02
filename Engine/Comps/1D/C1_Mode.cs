using System.Numerics;
using Engine.Assets;
using Engine.Comps._3D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

[Title("Game Mode")]
public class C1_GameMode : Imp1D
{
    [ImpVar][Category("Player")] public TClass<Imp3D> default_pawn;
    [ImpVar][Category("Player")] public TClass<C3_Camera> default_camera;
    [ImpVar][Category("Player")] public A_CameraConfig default_camera_config;

    [ImpVar][Category("State")] public List<TClass<C1_System>> systems_preload = new();
    [ImpVar][Category("State")] public TClass<C1_System> system_load;
    [ImpVar][Category("State")] public List<TClass<C1_System>> systems_postload = new();
    [ImpVar][Category("State")] public List<TClass<C1_System>> systems_persistent = new();
    [ImpVar][Category("State")] public double persistent_system_frequency = 0.2;

    public List<C1_System> active_states = new();

    private double _time_since_persistent;
    private bool _init_load_finished;


    public override void OnBegin()
    {
        base.OnBegin();
        ImpPlayer.Get().input_targets.Add(this);
    }

    public override void OnEnd()
    {
        base.OnEnd();
        ImpPlayer.Get().input_targets.Remove(this);
    }
}
