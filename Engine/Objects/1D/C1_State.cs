using ImperiumEngine.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects._1D;

//A base gameplay state
public class C1_State : ImpComponent
{
    protected override bool IsSingleton() => true;
    
    public TTags state_tags;
    public TTags blocked_states;
}