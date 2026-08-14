using ImperiumEngine.Assets;

namespace ImperiumEngine.Files;

public class File_PNG : ImpFile
{
    public File_PNG()
    {
        file_type = EFileType.Texture;
        default_asset_type = typeof(A_Texture);
    }
}