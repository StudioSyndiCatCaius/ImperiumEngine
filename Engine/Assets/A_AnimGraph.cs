using ImperiumEngine;

namespace ImperiumEngine.Assets;

public struct TAnimGraphPose
{
    
}

//Graph for playing animations on a C3_Skeleton
[AssetColor(215, 110, 190)]
public class A_AnimGraph : ImpAsset
{
    
}


public abstract class AnimGraphNode 
{
    
}

// ########################################################################################
// Graph Node
// ########################################################################################

public class AnimNode_Anim : AnimGraphNode 
{
    [ImpVar] public A_Animation animation;
}


public class AnimNode_Blend : AnimGraphNode 
{
    [ImpVar] public TAnimGraphPose pose_a;
    [ImpVar] public TAnimGraphPose pose_b;
    [ImpVar] public float blend;
}

public class AnimNode_BlendBool : AnimGraphNode 
{
    [ImpVar] public TAnimGraphPose pose_a;
    [ImpVar] public TAnimGraphPose pose_b;
    [ImpVar] public bool is_b;
    [ImpVar] public float blend_time;
}