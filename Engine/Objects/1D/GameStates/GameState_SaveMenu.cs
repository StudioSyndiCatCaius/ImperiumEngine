using ImperiumCore;

namespace ImperiumEngine.Objects._1D.GameStates;

public enum ESaveMenuType
{
    Save,
    Load,
}

public class GameState_SaveMenu
{
    [ImpVar][Pulse(Pulse.Read)] public ESaveMenuType type;
}