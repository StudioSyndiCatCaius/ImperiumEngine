using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Objects._3D;

public class C3_Sprite : ImpComponent3D
{
    [ImpVar] public bool is_billboard;

    [ImpVar] public bool use_anim_graph;
    [ImpVar] public A_AnimGraph_Sprite anim_graph;
}