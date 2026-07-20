using ImperiumEngine.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects._1D;

//A base gameplay state
public abstract class C1_GameState : ImpComponent
{
    protected override bool IsSingleton() => true;
    
    [ImpVar] public TTags state_tags;
    [ImpVar] public TTags blocked_states;
}