namespace ImperiumEngine.Main;

public enum EContentDir
{
    Game, Engine,
}

// statics for file & path handling
public class ImpPath
{

    public static string Path_ToAbsolute(string in_path)
    {
        string out_path = in_path;
        out_path=out_path.Replace("{game}",GetDir_Content());
        out_path=out_path.Replace("{engine}",GetDir_Content(EContentDir.Engine));

        return out_path;
    }

    public static string Path_ToLocal(string in_path)
    {
        string out_path = in_path;
        out_path=out_path.Replace(GetDir_Content(),"{game}");
        out_path=out_path.Replace(GetDir_Content(EContentDir.Engine),"{engine}");

        return out_path;
    }

    // Full resolution for a path that may use {game}/{engine} tokens, or be relative to
    // the file that referenced it (an asset's source_file, say).
    public static string Path_Resolve(string in_path, string? owner_file = null)
    {
        if (string.IsNullOrEmpty(in_path)) return "";

        string out_path = Path_ToAbsolute(in_path);

        if (!Path.IsPathRooted(out_path) && !string.IsNullOrEmpty(owner_file))
        {
            string dir = Path.GetDirectoryName(Path_ToAbsolute(owner_file)) ?? "";
            if (dir != "") out_path = Path.Combine(dir, out_path);
        }

        return Path.GetFullPath(out_path);
    }

    // Content ships next to the executable: Engine/_Content is copied to the output dir
    // by Engine.csproj, so these resolve the same in a dev run and a published build.
    public static string GetDir_Content(EContentDir dir=EContentDir.Game)
    {
        return dir switch
        {
            EContentDir.Engine => Path.Combine(AppContext.BaseDirectory, "_Content"),
            _                  => Path.Combine(AppContext.BaseDirectory, "Content"),
        };
    }

    public static string GetDir_Game()
    {
        return "Game";
    }
}
