namespace ImperiumEngine.Files;

public class File_TTF : ImpFile
{
    public File_TTF()
    {
        file_type = EFileType.Font;
        default_asset_type = typeof(ImperiumEngine.Assets.A_Font);
    }
}