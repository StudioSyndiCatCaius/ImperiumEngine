using Engine.Comps._1D.States;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

public class C1_GlobalScene : Imp1D
{
    public C1_StateManager main_states = new()
    {
        //starting_state =
        fallback_state = new TClass<C1_State>(typeof(sys_Explore))
    };
    
    public C1_GlobalScene()
    {
        Child_Add(main_states);
    }
}