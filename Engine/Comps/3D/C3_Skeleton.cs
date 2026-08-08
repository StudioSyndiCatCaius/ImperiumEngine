using ImperiumEngine.Assets;
using ImperiumEngine.Main;
using ImperiumEngine.Structs;
using R3D_cs;

namespace ImperiumEngine.Comps._3D;

public class C3_Skeleton : ImpComp3D
{
    [ImpVar][Export] A_Skeleton skeleton;
    [ImpVar][Export] List<C3_Mesh> bound_meshes; //meshes to animate with this skeleton
    [ImpVar][Export] A_AnimGraph anim_graph;
    
    private Dictionary<string, TTransform3> bone_transforms = new Dictionary<string, TTransform3>();
    private Dictionary<string, float> blend_shapes = new Dictionary<string, float>();

    public override void OnDraw(double dt, EDrawFlags flags)
    {
        base.OnDraw(dt, flags);
        
    }
}