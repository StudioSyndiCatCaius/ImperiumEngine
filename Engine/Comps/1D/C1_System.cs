using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

// A singleton that handles a slice of game state. Activate creates it under the
// live game mode; Shutdown / Kill tears it down and fires on_shutdown.
public abstract class C1_System : Imp1D
{
    // ################################################################################################################
    // STATIC
    // ################################################################################################################

    public static C1_System Activate(TClass<C1_System> Class, object context, Action on_shutdown=null)
    {
        return null;
    }

    public static void Shutdown(TClass<C1_System> system)
    {
        
    }

    public static bool IsActive(TClass<C1_System> system)
    {
        return false;
    }

    public static bool CanActivate(TClass<C1_System> system)
    {
        return false;   
    }

    public static bool IsTagActive(TTag tag)
    {
        return false;  
    }
    
    public static bool AreTagsBlocked(TTagSet tags)
    {
        return false;  
    }
    
    // ################################################################################################################
    // CLASS
    // ################################################################################################################

    [ImpVar] public TTagSet system_tags;
    [ImpVar] public TTagSet blocked_system_tags;

}
