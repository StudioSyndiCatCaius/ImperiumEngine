using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

// A singleton that handles a slice of game state. Activate creates it under the
// live game mode; Shutdown / Kill tears it down and fires on_shutdown.
public abstract class C1_System : Imp1D
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    private static C1_GameMode _GetGameMode()
    {
        return null;
    }
    
    public static C1_System Activate(TClass<C1_System> Class, object context, Action on_shutdown=null)
    {
        if(!CanActivate(Class)) return null;
        //first shutdown now invalid systems
        C1_System _sys=Activator.CreateInstance(Class.Get(), context) as C1_System;
        _GetGameMode().active_states.Add(_sys);
        
        foreach (var s in _GetGameMode().active_states)
        {
            if (AreTagsBlocked(s.system_tags))
            {
                s.Kill();
            }
        }
        _GetGameMode().Child_Add(_sys);
        return _sys;
    }

    public static void SetListActive(List<TClass<C1_System>> Classes, bool active, object context)
    {
        foreach (TClass<C1_System> s in Classes)
        {
            if (active) Activate(s, context);
            else Shutdown(s);
        }
    }

    public static void Shutdown(TClass<C1_System> system)
    {
        foreach (var s in _GetGameMode().active_states)
        {
            if (s.GetType() == system.Get())
            {
                s.Kill();
            }
        }
    }

    public static bool IsActive(TClass<C1_System> system)
    {
        foreach (var s in _GetGameMode().active_states)
        {
            if (s.GetType() == system.Get())
            {
                return true;
            }
        }
        return false;
    }

    public static bool CanActivate(TClass<C1_System> system)
    {
        if(IsActive(system)) return false;
        C1_System _default=null; //not sure how to get this yet
        if(AreTagsBlocked(_default.system_tags)) return false;
        return true;
    }

    public static bool IsTagActive(TTag tag)
    {
        foreach (var s in _GetGameMode().active_states)
        {
            if (s.system_tags.Has(tag))
            {
                return true;
            }
        }
        return false;  
    }
    
    public static bool AreTagsBlocked(TTagSet tags)
    {
        foreach (var s in _GetGameMode().active_states)
        {
            if (s.blocked_system_tags.HasAny(tags))
            {
                return true;
            }
        }
        return false; 
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    [ImpVar] public TTagSet system_tags;
    [ImpVar] public TTagSet blocked_system_tags;

}
