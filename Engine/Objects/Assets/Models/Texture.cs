using ImperiumCore.Classes;
using ImperiumCore.Structs;
using StbImageSharp;

namespace ImperiumCore.Assets;

// ---------------------------------------------------------------------------------------------------------------------
// BASE
// ---------------------------------------------------------------------------------------------------------------------

public abstract class A_Texture : ModelRef
{
    [ImpVar][Exposed] public TModel_Texture ModelTexture;
    
    public A_Texture()
    {
        ModelType = EModelRefType.Texture;
    }
}

// ---------------------------------------------------------------------------------------
// Texture 2D
// ---------------------------------------------------------------------------------------

public class A_Texture2D : A_Texture
{
    public override bool Import_Try(string filepath)
    {
        if (!File.Exists(filepath)) return false;

        using var _stream = File.OpenRead(filepath);
        var _img = ImageResult.FromStream(_stream, ColorComponents.RedGreenBlueAlpha);

        ModelTexture.width = _img.Width;
        ModelTexture.height = _img.Height;
        ModelTexture.pixels = _img.Data;
        return true;
    }
}


// ---------------------------------------------------------------------------------------
// Render Target
// ---------------------------------------------------------------------------------------

public class A_RenderTarget : A_Texture
{
    public System.Drawing.Color backgroundColor = System.Drawing.Color.FromArgb(255, 26, 26, 38);
}
