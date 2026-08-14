using Raylib_cs;

namespace ImperiumEngine.Assets;

[AssetColor(160, 90, 210)]
public class A_Font : ImpAsset
{
    // #################################################################################
    // Class
    // #################################################################################
    public Font font;
    [ImpVar] public TextureFilter filter=TextureFilter.Bilinear;

    public override void Source_OnReload(ImpFile file)
    {
        base.Source_OnReload(file);
        int i = source_index;
        if (i < 0 || i >= file.src_fonts.Count) return;
        font=file.src_fonts[i];
        Raylib.SetTextureFilter(font.Texture, filter);
    }
    
    // #################################################################################
    // Static
    // #################################################################################
    
    public static A_Font FONT_ARIAL=Import<A_Font>("{engine}/Fonts/Arial.ttf");
    public static A_Font FONT_ARIAL_B=Import<A_Font>("{engine}/Fonts/Arial_Bold.ttf");
    public static A_Font FONT_ARIAL_I=Import<A_Font>("{engine}/Fonts/Arial_Italic.ttf");
    public static A_Font FONT_ARIAL_BI=Import<A_Font>("{engine}/Fonts/Arial_Bold_Italic.ttf");
    
    public static A_Font FONT_TENDERNESS=Import<A_Font>("{engine}/Fonts/tenderness.otf");
    
}