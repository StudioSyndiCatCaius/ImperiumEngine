using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
using ImperiumCore.Assets;
using ImperiumCore.Classes;

namespace ImperiumCore.Structs;

// ================================================================================================
// SHAPE
// ================================================================================================

public class TBrush_Shape
{
    [ImpVar][Exposed] public Color    color = Color.White;
    [ImpVar][Exposed] public TVector2b cornerRadius;

    [ImpVar][Exposed] public Color    strokeColor;
    [ImpVar][Exposed] public TVector2b strokeWidth;

    [ImpVar][Exposed] public Color    shadowColor;
    [ImpVar][Exposed] public TVector2b shadowOffset;
    [ImpVar][Exposed] public byte     shadowSharpness;

    public void Draw(ImpRender rhi, Vector2 position, Vector2 size)
    {
        var dl       = ImGui.GetWindowDrawList();
        float radius = (cornerRadius.x + cornerRadius.y) * 0.5f;
        dl.AddRectFilled(position, position + size, BrushHelper.ToU32(color), radius);

        float sw = Math.Max(strokeWidth.x, strokeWidth.y);
        if (sw > 0)
            dl.AddRect(position, position + size, BrushHelper.ToU32(strokeColor), radius, 0, sw);
    }
}

// ================================================================================================
// IMAGE
// ================================================================================================

public class TBrush_Image
{
    [ImpVar][Exposed] public A_Texture2D?      texture;
    [ImpVar][Exposed] public Color             tint   = Color.White;
    [ImpVar][Exposed] public EBrushImageFormat format;

    public void Draw(ImpRender rhi, Vector2 position, Vector2 size)
    {
        if (texture == null) return;
        // TODO: wire AddImage once ImTextureRef bridge is in place
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(position, position + size, BrushHelper.ToU32(tint));
    }
}

public enum EBrushImageFormat { Image, NineSlice, Tile }

// ================================================================================================
// TEXT
// ================================================================================================

public class TBrush_Text
{
    [ImpVar][Exposed] public string  text     = "";
    [ImpVar][Exposed] public A_Font? font;
    [ImpVar][Exposed] public int     fontSize = 16;

    public void Draw(ImpRender rhi, Vector2 position, Vector2 size)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dl = ImGui.GetWindowDrawList();
        dl.AddText(position, 0xFFFFFFFF, text);
    }
}

// ================================================================================================

file static class BrushHelper
{
    public static uint ToU32(Color c) => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
}
