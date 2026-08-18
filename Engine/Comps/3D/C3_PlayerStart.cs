using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_PlayerStart : Imp3D
{
    // #################################################################################
    // Static
    // #################################################################################

    public static C3_PlayerStart GetFirstOfSceneLink(TRef<ImpScene> scene, int id = -1)
    {
        return null;
    }
    
    // #################################################################################
    // Class
    // #################################################################################
    
    [ImpVar] public TRef<ImpScene> linked_scene;
    [ImpVar] public int player_index = 0;

}