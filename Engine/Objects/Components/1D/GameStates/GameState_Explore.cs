using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects._1D.GameStates;

public class GameState_Explore : ImpGameState
{
    public TTag input_pause;
    public TTag input_move;
    public TTag input_camera;
}