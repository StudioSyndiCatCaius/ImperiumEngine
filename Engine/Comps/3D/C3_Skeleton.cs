using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using R3D_cs;

namespace ImperiumEngine.Comps._3D;

public class C3_Skeleton : Imp3D
{
    [ImpVar]  A_Skeleton skeleton;
    [ImpVar]  List<C3_Mesh> bound_meshes; //meshes to animate with this skeleton
    [ImpVar]  A_AnimGraph anim_graph;
    
    private Dictionary<string, TTransform3> bone_transforms = new Dictionary<string, TTransform3>();
    private Dictionary<string, float> blend_shapes = new Dictionary<string, float>();

}