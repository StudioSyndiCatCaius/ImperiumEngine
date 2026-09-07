using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

public class C1_StateManager : Imp1D
{
    [ImpVar] public TClass<C1_State> fallback_state; // State to switch to if no other state is active
    [ImpVar] public TClass<C1_State> starting_state; // State to switch to when the comp starts
    
    private C1_State _current_state;

    public C1_State State_Start(TClass<C1_State> state)
    {
        return null;
    }
    public void State_Stop() // stop current state
    {
        
    }
}