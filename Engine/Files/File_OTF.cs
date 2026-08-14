namespace ImperiumEngine.Files;

public class File_OTF : ImpFile
{
    public File_OTF()
    {
        file_type = EFileType.Font;
        default_asset_type = typeof(ImperiumEngine.Assets.A_Font);
    }
}