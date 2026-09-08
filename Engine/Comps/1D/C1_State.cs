using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

// A singleton that handles a slice of game state. Activate creates it under the
// live game mode; Shutdown / Kill tears it down and fires on_shutdown.
public abstract class C1_State : Imp1D
{
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    [ImpVar] public TTagSet system_tags;
    [ImpVar] public TTagSet blocked_system_tags;

    [ImpVar] public C1_StateManager substates = new();

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
