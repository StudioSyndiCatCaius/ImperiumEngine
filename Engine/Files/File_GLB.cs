using ImperiumEngine.Assets;

namespace ImperiumEngine.Files;

public class File_GLB : ImpFile
{
    public File_GLB()
    {
        file_type = EFileType.Model;
        default_asset_type = typeof(A_Mesh);
    }
}
