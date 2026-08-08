using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Objects._3D;

public class C3_Skeleton : ImpPhysic3D
{
    [ImpVar] public A_Skeleton skeleton;
    
    //meshes that will have any matching bone weights & blend-shapes animated from this skeleton
    [ImpVar] public List<A_Mesh> linked_meshes;
    [ImpVar] public A_Animation anim_default;
    [ImpVar] public bool anim_looping=true;
    [ImpVar] public A_AnimGraph_Skeletal anim_graph;
    [ImpVar] public float anim_speed=1.0f;
}


