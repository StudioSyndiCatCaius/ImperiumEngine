using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Assets;

public class A_Font : ImpAsset
{
    //pixel size the glyph atlas is rasterised at; text scales down from this
    [ImpVar] public int base_size = 48;

    protected override ImpResource? Resource_Create(string full_path)
    {
        return new ImpResource_Font { base_size = base_size };
    }
}

public class ImpResource_Font : ImpResource
{
    public int base_size = 48;

    public override void OnImport(string filepath)
    {
        if (!File.Exists(filepath)) return;

        var font = Raylib.LoadFontEx(filepath, base_size, null, 0);
        if (font.Texture.Id == 0) return;

        // The atlas is rasterised once at base_size and UI text is drawn well under it (16pt
        // off a 48pt atlas), so a single mip sampled bilinearly still undersamples and leaves
        // glyph edges ragged. Mips give the minification something to blend between.
        Raylib.GenTextureMipmaps(ref font.Texture);
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Trilinear);

        r_font.Add(font);
    }
}
