using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

// a game system is a singleton that handles certain events and game state code. they can run asmot liek an evetn with a Activate & callbac on finish
public abstract class C1_GameSystem : ImpComp
{
    // ################################################################################################################
    // STATIC
    // ################################################################################################################

    public static C1_GameSystem GetActive(TClass<C1_GameSystem> system)
    {
        foreach (var s in ImpGame.current.game_mode.active_states)
        {
            if (s.GetType() == system.Get())
            {
                return s;
            }
        }
        return null;
    }
    
    public static C1_GameSystem Activate(TClass<C1_GameSystem> system, Action on_shutdown=null)
    {
        if(IsActive(system)) { return null; }

        C1_GameSystem _sys = null; // how do?
        ImpGame.current.game_mode.active_states.Add(_sys);
        ImpGame.current.game_mode.Child_Add(_sys);
        return _sys;
    }

    public static void SetListActive(List<TClass<C1_GameSystem>> systems, bool active)
    {
        foreach (var s in systems)
        {
            if (active)
            {
                Activate(s);
            }
            else
            {
                Shutdown(s);
            }
        }
    }
    
    public static bool Shutdown(TClass<C1_GameSystem> system)
    {
        C1_GameSystem _sys = GetActive(system);
        if(_sys==null) { return false; }
        _sys.Destroy();
        ImpGame.current.game_mode.active_states.Remove(_sys);
        return true;
    }
    
    public static bool IsActive(TClass<C1_GameSystem> system)
    {
        if(GetActive(system) == null) { return false; }
        return true;
    }
    
    // ################################################################################################################
    // CLASS
    // ################################################################################################################
    
    [ImpVar] [Category("Tags")] private TTagSet system_tags;
    //Systems that will be shutdown and blocked from starting while this one is active
    [ImpVar] [Category("Tags")] private TTagSet blocked_systems;
    
}