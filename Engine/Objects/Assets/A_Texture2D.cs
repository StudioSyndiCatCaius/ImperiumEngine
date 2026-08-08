using ImperiumEngine.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects.Assets;

public abstract class A_Texture : ImpAsset
{
    
}

public class A_Texture2D : A_Texture
{
    [ImpVar] public float hue_shift;
    [ImpVar] public float saturation=1.0f;
    [ImpVar] public float brightness = 1.0f;
    
    [ImpVar] public bool NineSlice;
    [ImpVar] public TMargins2D NineSliceMargins;
}



public class A_RenderTarget : A_Texture
{
    
}