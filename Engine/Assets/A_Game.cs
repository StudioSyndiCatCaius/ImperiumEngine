using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Assets;

[AssetColor(230, 190, 50)]
public class A_Game : ImpAsset , I_General
{
    public static A_Game GAME_TEST = new()
    {
        title = new("Test Game"),
        gamepath = "Projects/Test/Test.ImpGame",
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
        // gamepath is the project .ImpGame. filepath is often stamped builtin:A_Game.GAME_TEST
        // by Builtins_All — that is not a folder and must not win over gamepath.
        string p = gamepath;
        if (string.IsNullOrWhiteSpace(p) || ImpAsset.Path_IsBuiltin(p))
        {
            p = filepath;
        }
        if (string.IsNullOrWhiteSpace(p) || ImpAsset.Path_IsBuiltin(p))
        {
            return "";
        }
        if (!Path.IsPathRooted(p))
        {
            string engine_dir = Path.GetDirectoryName(ImpFile.ContentDir_Engine());
            string root = Path.GetDirectoryName(engine_dir);
            if (!string.IsNullOrEmpty(root))
            {
                p = Path.GetFullPath(Path.Combine(root, p));
            }
        }
        if (Directory.Exists(p))
        {
            return p;
        }
        string dir = Path.GetDirectoryName(p);
        return string.IsNullOrEmpty(dir) ? "" : dir;
    }
    
    public override string File_GetExtension() { return "ImpGame"; }
}