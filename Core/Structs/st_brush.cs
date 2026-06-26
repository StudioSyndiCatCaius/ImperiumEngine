using System.Drawing;
using System.Numerics;
using ImperiumCore.Assets;
using ImperiumCore.Classes;

namespace ImperiumCore.Structs;

// ================================================================================================
// SHAPE
// ================================================================================================

public class TBrush_Shape
{
    [ImpVar][Exposed] Color color=Color.White;
    [ImpVar][Exposed] TVector2b cornerRadius;
    
    [ImpVar][Exposed] Color strokeColor;
    [ImpVar][Exposed] TVector2b strokeWidth;
    
    [ImpVar][Exposed] Color shadowColor;
    [ImpVar][Exposed] TVector2b shadowOffset;
    [ImpVar][Exposed] byte shadowSharpness;
    
    public void Draw(ImpRender rhi, Vector2 position, Vector2 size)
    {
        
    }
}

public enum EBrushImageFormat
{
    Image,
    NineSlice,
    Tile
}

// ================================================================================================
// IMAGE
// ================================================================================================

public class TBrush_Image
{
    [ImpVar] [Exposed] public A_Texture2D? texture;
    [ImpVar] [Exposed] public Color tint = Color.White;
    [ImpVar] [Exposed] public EBrushImageFormat format;

    public void Draw(ImpRender rhi, Vector2 position, Vector2 size)
    {
        
    }
}

// ================================================================================================
// IMAGE
// ================================================================================================

public class TBrush_Text
{
    [ImpVar][Exposed] public string text;
    [ImpVar][Exposed] public A_Font font;
    [ImpVar][Exposed] public int fontSize;

    public void Draw(ImpRender rhi, Vector2 position, Vector2 size)
    {
        
    }
}