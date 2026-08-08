using System.Numerics;
using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects.Assets;

public struct TBlendSpaceKey
{
    public Vector2 position;
    public A_Animation animation;
}


public abstract class A_BlendSpace : ImpAsset
{
    public List<TBlendSpaceKey> keys;
}

//A blend space for 3D (Skeletal) animations
public class A_BlendSpace_Skeletal : A_BlendSpace
{
    
}


//A blend space for 2D (Sprite) animations
public class A_BlendSpace_Sprite : A_BlendSpace
{
    
}