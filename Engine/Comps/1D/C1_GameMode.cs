using ImperiumEngine.Comps._3D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public abstract class C1_GameMode : ImpComp
{
    
    [ImpVar][Category("Player")] public TClass<Imp3D> default_pawn;
    [ImpVar][Category("Player")] public TClass<C3_Camera> default_camera;
    
    [ImpVar][Category("State")] public List<TClass<C1_GameSystem>> systems_preload;
    [ImpVar][Category("State")] public TClass<C1_GameSystem> system_load;
    [ImpVar][Category("State")] public List<TClass<C1_GameSystem>> systems_postload;
    [ImpVar][Category("State")] public List<TClass<C1_GameSystem>> systems_persistent;
    [ImpVar][Category("State")] public double persistent_system_frequency=0.2;
    
    public List<C1_GameSystem> active_states;

    private double _time_since_persistent;
    private bool _init_load_finished;
    
    public override void OnBegin()
    {
        base.OnBegin();
        C1_GameSystem.SetListActive(systems_persistent,true);

        //spawn and set player pawns
        foreach (var ply in ImpPlayer.players)
        {
            Imp3D _spawned_pawn = null; // spawn from game mode
            ply.pawn=_spawned_pawn;
            //get first comp of type
            
            //set to spawn point by id
            C3_PlayerStart? _start= C3_PlayerStart.GetFirstOfSceneLink(new(ImpGame.current.scene_previous));
            if (_start != null)
            {
                _spawned_pawn.Transform_Set(_start.Transform_Get(true),true);
            }
        }
        
        C1_GameSystem.Activate(system_load, () =>
        {
            C1_GameSystem.SetListActive(systems_postload,true);
            _time_since_persistent=persistent_system_frequency;
            _init_load_finished=true;
        });
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (_init_load_finished)
        {
            if (_time_since_persistent <= 0.0)
            {
                C1_GameSystem.SetListActive(systems_persistent,true);
                _time_since_persistent = persistent_system_frequency;
            }
            else
            {
                _time_since_persistent -= dt;
            }
        }
        
    }
}