using Engine.Comps._1D.Systems;
using Engine.Assets;
using Engine.Comps._3D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D.Modes;

[Title("Mode : Gameplay")]
public abstract class GM_Gameplay : C1_GameMode
{
    public GM_Gameplay()
    {
        default_pawn = new TClass<Imp3D>(typeof(C3_Character));
    }
}
