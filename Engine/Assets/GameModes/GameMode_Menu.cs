using Engine.Comps._1D;
using Engine.Comps._3D;
using Engine.Structs;

namespace Engine.Assets.GameModes;

public class GM_Menu : A_GameMode
{
    [ImpVar] public C3_Camera camera;
    
    
    
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [Builtin] public static GM_Menu DEFAULT = new();
}