using System.Numerics;
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
    [ImpVar] public A_SkeletonAnim DefaultSkeletonAnim;
    [ImpVar] public A_SkeletalAnimator anim_graph;

    public Dictionary<TLabel, float> params_float=new ();
    public Dictionary<TLabel, bool> params_bool=new ();
    public Dictionary<TLabel, Vector2> params_V2=new ();
    public Dictionary<TLabel, Vector3> params_V3=new ();
    
    public override void OnDraw3D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw3D(dt, flags);
        Skeleton sk;
        BoneInfo inf;
        
    }


    public void PlayAnimation(A_SkeletonAnim anim, Action<TLabel> on_notify=null, Action on_complete=null)
    {
        
    }
}