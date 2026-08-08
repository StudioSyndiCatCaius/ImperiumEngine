using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects._3D;

// a position in the level where a player will spawn its pawn entity class
public class C3_PlayerStart : ImpComponent3D
{
    [ImpVar] public int player_id;
}