using ImperiumEngine;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Assets;

public struct TSkeletonPose
{
    public Dictionary<TLabel,TTransform3> bone_transforms;
    public Dictionary<TLabel,float> blendshapes;

    public static TSkeletonPose Blend2(TSkeletonPose a, TSkeletonPose b, float blend)
    {
        return new();
    }
    
}

[AssetColor(195, 130, 150)]
public class A_Skeleton : ImpAsset
{
    
}