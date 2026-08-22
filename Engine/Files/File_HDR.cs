using ImperiumEngine.Assets;

namespace ImperiumEngine.Files;

public class File_HDR : ImpFile
{
    public File_HDR()
    {
        file_type = EFileType.Texture;
        default_asset_type = typeof(A_TextureHDR);
    }
}
