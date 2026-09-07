using Engine.Comps._3D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Assets.GameModes;

//A Rail Shooter base game mode
[Title("Game Mode : Rail Mover")]
public class GameMode_RailMover : A_GameMode
{
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public TClass<Imp3D> GetPawnClass()
    {
        if(player_pawn.Get()!=null) return player_pawn;
        return GameMode_Common.default_pawn;
    }
    
    [ImpVar][Category("Player")] public TClass<Imp3D> player_pawn;
    [ImpVar][Category("Player")] public bool pawn_can_move_Forward=true;
    [ImpVar][Category("Player")] public bool pawn_can_move_Sideways=true;

    [ImpVar][Category("Path")] public TRef<C3_Spline> path_spline;
    
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar][Config] public static TClass<Imp3D> default_pawn= new TClass<Imp3D>(typeof(C3_Character));

}