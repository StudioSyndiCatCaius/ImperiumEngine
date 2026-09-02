using Engine.Core;
using Engine.Structs;

namespace Engine.Assets;

public struct TAnimationPose
{
    public Dictionary<TLabel,TTransform3> bone_transforms;
}

public class A_Animation : ImpAsset
{
    [ImpVar] public int source_id = 0;
}