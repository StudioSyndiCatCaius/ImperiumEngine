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

        // the atlas is rasterised once at base_size, so filter it for the common case of
        // drawing smaller than that - otherwise downscaled glyphs alias badly
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);

        r_font.Add(font);
    }
}
