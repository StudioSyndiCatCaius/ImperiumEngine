using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Assets;

[AssetColor(230, 190, 50)]
public class A_Game : ImpAsset , I_General
{
    public static A_Game GAME_TEST = new()
    {
        title = new("Test Game"),
        gamepath = "D:\\PROJECTS\\ImperiumEngine\\ImperiumEngine\\Projects\\Test\\Test.ImpGame",
    };
    public static A_Game game = GAME_TEST;

    [ImpVar] public TText title;
    [ImpVar] public string gamepath;

    public string GetName()
    {
        return Path.GetFileNameWithoutExtension(filepath);
    }

    public string GetRootDir()
    {
        string p = !string.IsNullOrEmpty(filepath) ? filepath : gamepath;
        if (string.IsNullOrWhiteSpace(p)) return "";
        return Path.GetDirectoryName(p) ?? "";
    }
    
    public override string File_GetExtension() { return "ImpGame"; }
}