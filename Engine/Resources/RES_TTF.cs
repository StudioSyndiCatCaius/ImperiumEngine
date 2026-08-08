using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Resources;

public class RES_TTF : ImpResource
{
    public override void OnImport(string filepath)
    {
        if (!File.Exists(filepath))
            throw new FileNotFoundException($"Font not found: {filepath}");

        Font font = Raylib.LoadFontEx(filepath, 12, null, 0);
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Anisotropic4X);
        r_font.Add(font);
    }
}