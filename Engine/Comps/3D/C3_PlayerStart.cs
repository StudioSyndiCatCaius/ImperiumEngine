using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

public class C3_PlayerStart : ImpComp3D
{
    [ImpVar] public TRef<ImpScene> linked_scene;
    [ImpVar] public int player_index = 0;

}