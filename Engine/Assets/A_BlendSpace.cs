using Engine.Core;

namespace Engine.Assets;

public struct TBlendSpaceKey
{
    public A_Animation animation;
    public float x;
    public float y;
}

public class A_BlendSpace : ImpAsset
{
    public List<TBlendSpaceKey> keys;
}