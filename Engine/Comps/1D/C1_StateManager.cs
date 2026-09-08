using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

public class C1_StateManager : Imp1D
{
    [ImpVar] public TClass<C1_State> fallback_state; // State to switch to if no other state is active
    [ImpVar] public TClass<C1_State> starting_state; // State to switch to when the comp starts
    
    private C1_State _current_state;

    public override void OnBegin()
    {
        base.OnBegin();
        State_Start(starting_state);
    }

    public C1_State State_Start(TClass<C1_State> state)
    {
        Type type = state.Get();
        if (type == null)
        {
            State_Update();
            return _current_state;
        }
        if (_current_state != null)
        {
            Child_Remove(_current_state);
            _current_state = null;
        }
        C1_State st = Activator.CreateInstance(type) as C1_State;
        _current_state = st;
        if (st != null) Child_Add(st);
        return st;
    }
    public void State_Stop() // stop current state
    {
        if (_current_state != null)
        {
            Child_Remove(_current_state);
            _current_state = null;
        }
        State_Update();
    }
    
    public void State_Update()
    {
        if (_current_state != null) return;
        if (fallback_state.Get() == null) return;
        State_Start(fallback_state);
    }
}