using Engine.Assets;
using Engine.Core;
using Engine.Structs;
using R3D_cs;

namespace Engine.Comps._3D;

public struct TSkeletonPose
{
    public Dictionary<TLabel,TTransform3> bone_transforms;
    public Dictionary<TLabel,float> blendshapes;
}

public class C3_Skeleton : Imp3D
{
    [ImpVar] public C3_Mesh[] meshes; //meshes whoses boneweights are driven by this 
    [ImpVar] public A_Skeleton skeleton;
    [ImpVar] public A_SkeletalAnimGraph anim_graph;
    
    public override void OnDraw3D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw3D(dt, flags);
        Skeleton sk;
        BoneInfo inf;
        
    }


    public void PlayAnimation(A_Animation anim, Action<TLabel> on_notify=null, Action on_complete=null)
    {
        
    }
}