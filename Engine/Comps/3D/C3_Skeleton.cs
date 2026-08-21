using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using R3D_cs;

namespace ImperiumEngine.Comps._3D;

public class C3_Skeleton : Imp3D
{
    [ImpVar]  A_Skeleton skeleton;
    [ImpVar]  List<C3_Mesh> bound_meshes; //meshes to animate with this skeleton
    [ImpVar]  A_Animation default_animation;
    [ImpVar]  A_AnimGraph anim_graph;
    
    public Dictionary<TLabel,TSkeletonPose> cached_poses = new Dictionary<TLabel,TSkeletonPose>();
    


}