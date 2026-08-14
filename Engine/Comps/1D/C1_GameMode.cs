using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public class C1_GameMode : ImpComp
{
    public static C1_GameMode current;
    
    [ImpVar] public TClass<C1_GameState> starting_state;
    [ImpVar] public TClass<C1_GameState> default_state;
}