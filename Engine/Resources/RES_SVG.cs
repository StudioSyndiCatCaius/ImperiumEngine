using ImperiumEngine.Main;
using Raylib_cs;
using SkiaSharp;
using Svg.Skia;

namespace ImperiumEngine.Resources;

public class RES_SVG : ImpResource
{
    // Class icons and other UI vectors are shown at small, fixed sizes, so rasterizing
    // once at a flat resolution (rather than whatever size the source document happens
    // to use) keeps every icon consistent regardless of how it was authored.
    const int RASTER_SIZE = 128;

    public override void OnImport(string filepath)
    {
        base.OnImport(filepath);
        if (!File.Exists(filepath)) return;

        using var svg = new SKSvg();
        var picture = svg.Load(filepath);
        if (picture == null) return;

        float extent = MathF.Max(picture.CullRect.Width, picture.CullRect.Height);
        float scale = extent > 0 ? RASTER_SIZE / extent : 1f;

        using var stream = new MemoryStream();
        if (!svg.Save(stream, SKColors.Transparent, SKEncodedImageFormat.Png, 100, scale, scale)) return;

        Image image = Raylib.LoadImageFromMemory(".png", stream.ToArray());
        Texture2D texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);

        if (texture.Id == 0) return;

        // Icons are rasterised far larger than they are drawn (a tree row shows this at
        // ~16px), and minifying that far off a single mip with point sampling is what makes
        // thin vector strokes crawl and break up. Mips + trilinear resolve the scale down.
        Raylib.GenTextureMipmaps(ref texture);
        Raylib.SetTextureFilter(texture, TextureFilter.Trilinear);

        r_texture.Add(texture);
    }
}
