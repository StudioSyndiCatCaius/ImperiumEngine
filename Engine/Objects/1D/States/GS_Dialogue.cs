using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects._1D.States;

public class GS_Dialogue : C1_GameState
{
    

    //Label for input action that opens the pause state
    [ImpVar] public TLabel Pause_Input;
    //pause state class that is opened
    [ImpVar] public TRef<C1_GameState> Pause_State;
}