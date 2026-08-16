using ImperiumEngine.Comps._3D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public abstract class C1_GameMode : ImpComp
{
    
    [ImpVar][Category("Player")] public TClass<Imp3D> default_pawn;
    [ImpVar][Category("Player")] public TClass<C3_Camera> default_camera;
    
    [ImpVar][Category("State")] public TClass<C1_GameState> starting_state;
    [ImpVar][Category("State")] public TClass<C1_GameState> default_state;
}