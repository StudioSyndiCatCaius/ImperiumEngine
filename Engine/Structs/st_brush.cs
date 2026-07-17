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
        var pos = TVector2i.From(position);
        var sz  = TVector2i.From(size);
        float radius = MathF.Max(cornerRadius.x, cornerRadius.y);

        if (shadowColor.A > 0)
            rhi.Draw2D_RectRounded(new TVector2i(pos.x + shadowOffset.x, pos.y + shadowOffset.y), sz, shadowColor, radius);

        rhi.Draw2D_RectRounded(pos, sz, color, radius);
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
        if (texture == null) return;
        rhi.Draw2D_Texture(texture, TVector2i.From(position), TVector2i.From(size), tint);
    }
}

// ================================================================================================
// TEXT
// ================================================================================================

public class TBrush_Text
{
    [ImpVar][Exposed] public string text;
    [ImpVar][Exposed] public A_Font font;
    [ImpVar][Exposed] public int fontSize;
    [ImpVar][Exposed] public Color color = Color.White;

    public void Draw(ImpRender rhi, Vector2 position, Vector2 size)
    {
        if (string.IsNullOrEmpty(text)) return;
        rhi.Draw2D_Text(TVector2i.From(position), text, fontSize, color);
    }
}