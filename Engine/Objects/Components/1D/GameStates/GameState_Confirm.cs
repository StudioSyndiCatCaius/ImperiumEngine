using ImperiumCore;
using ImperiumCore.Classes.Components;

namespace ImperiumEngine.Objects._1D.GameStates;

public class GameState_Confirm : ImpGameState
{
    [ImpVar][Exposed] public string text_body;
    [ImpVar][Exposed] public string text_yes;
    [ImpVar][Exposed] public string text_no;
}