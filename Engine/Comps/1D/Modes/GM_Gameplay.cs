using ImperiumEngine.Comps._1D.States;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D.Modes;

public class GM_Gameplay : C1_GameMode
{
    public GM_Gameplay()
    {
        default_pawn = new(typeof(C3_Character));
        default_camera = new(typeof(C3_Camera));

        systems_persistent =
        [
            new(typeof(sys_Explore))
        ];
    }
}