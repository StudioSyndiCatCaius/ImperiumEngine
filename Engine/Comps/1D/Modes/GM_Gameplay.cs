using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D.States;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D.Modes;

public class GM_Gameplay : C1_GameMode
{
    public GM_Gameplay()
    {
        default_pawn = new TClass<Imp3D>(typeof(C3_Character));
        default_camera = new TClass<C3_Camera>(typeof(C3_Camera));
        default_camera_config = A_CameraConfig.CAM_THIRDPERSON.Clone() as A_CameraConfig;

        systems_persistent =
        [
            new TClass<C1_GameSystem>(typeof(sys_Explore))
        ];
    }
}
