using Engine.Comps._3D;
using Engine.Core;

namespace Engine.Assets.SkelAnim;

public class SkelAnim_Graph : A_SkeletalAnimator
{
    [ImpVar(Hidden = true)] public SkelAnim_Graph_Node[] nodes;
    
    public C3_Skeleton owning_comp=null;
}

public abstract class SkelAnim_Graph_Node : ImpAsset
{
    public SkelAnim_Graph owning_graph=null;
    
    public virtual TSkelAnimPose ProcessAnimation()
    {
        return new TSkelAnimPose();
    }
}

// ###################################################################################################################
// Graph Nodes
// ###################################################################################################################

public class AGN_Play_Anim : SkelAnim_Graph_Node
{
    [ImpVar] public A_SkeletonAnim SkeletonAnim;
}

public class AGN_Play_BlendSpace : SkelAnim_Graph_Node
{
    [ImpVar] public A_BlendSpace blend_space;
    [ImpVar] public float X;
    [ImpVar] public float Y;
}

public class AGN_Blend_Float : SkelAnim_Graph_Node
{
    [ImpVar] public TSkelAnimPose A;
    [ImpVar] public TSkelAnimPose B;
    [ImpVar] public float blend;
    public override TSkelAnimPose ProcessAnimation()
    {
        return TSkelAnimPose.Blend(A, B, blend);
    }
}

public class AGN_Blend_Bool : SkelAnim_Graph_Node
{
    [ImpVar] public TSkelAnimPose A;
    [ImpVar] public TSkelAnimPose B;
    [ImpVar] public bool blend;
    [ImpVar] public float blend_time=0.2f;
}

public class AGN_GetParam_Float : SkelAnim_Graph_Node
{
    
}

public class AGN_GetParam_Bool : SkelAnim_Graph_Node
{
    
}

public class AGN_GetParam_Vector2 : SkelAnim_Graph_Node
{
    
}

public class AGN_GetParam_Vector3 : SkelAnim_Graph_Node
{
    
}