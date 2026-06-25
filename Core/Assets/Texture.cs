using ImperiumCore.Classes;
using ImperiumCore.Structs;
using StbImageSharp;

namespace ImperiumCore.Assets;

// ---------------------------------------------------------------------------------------------------------------------
// BASE
// ---------------------------------------------------------------------------------------------------------------------



public abstract class A_Texture : ImpAsset
{
    [ImpVar][Exposed] public TTextureData textureData;
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

        textureData.width = _img.Width;
        textureData.height = _img.Height;
        textureData.pixels = _img.Data;
        return true;
    }
}


// ---------------------------------------------------------------------------------------
// Render Target
// ---------------------------------------------------------------------------------------

public class A_RenderTarget : A_Texture
{

}
