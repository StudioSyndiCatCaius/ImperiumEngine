namespace ImperiumCore.Classes;

public class ImpFile
{
    private static string? _engineDir;

    public static string CorrectPath(string path)
    {
        string result = path;

        result = result.Replace("{project}", Dir_Project());
        result = result.Replace("{Project}", Dir_Project());
        result = result.Replace("{engine}", Dir_Engine());
        result = result.Replace("{Engine}", Dir_Engine());

        return result;
    }

    // -------------------------------------------------------------------------------------------
    // Project
    // -------------------------------------------------------------------------------------------

    public static string Dir_Project()
    {
        return "";  //project root path (set once a game project is loaded)
    }

    public static string Dir_ProjectContent() { return Dir_Project() + "/Content/"; }
    public static string Dir_ProjectConfig() { return Dir_Project() + "/Config/"; }

    // -------------------------------------------------------------------------------------------
    // Engine
    // -------------------------------------------------------------------------------------------

    // Resolves the engine root for both layouts:
    //   - dev/editor: run out of .../Editor/bin/<cfg>/<tfm>; the source "Engine/Content"
    //     lives up the tree, so edits to content show without a copy step.
    //   - built/shipped: Content is copied next to the executable (no source tree),
    //     so the engine root is just the app base directory.
    public static string Dir_Engine()
    {
        if (_engineDir != null) return _engineDir;

        var _dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (_dir != null)
        {
            if (Directory.Exists(Path.Combine(_dir.FullName, "Engine", "Content")))
            {
                _engineDir = Path.Combine(_dir.FullName, "Engine");
                return _engineDir;
            }
            _dir = _dir.Parent;
        }

        _engineDir = AppContext.BaseDirectory.TrimEnd('/', '\\');
        return _engineDir;
    }

    public static string Dir_EngineContent() { return Dir_Engine() + "/Content/"; }
    public static string Dir_EngineConfig() { return Dir_Engine() + "/Config/"; }

    // -------------------------------------------------------------------------------------------
    // IMPORT
    // -------------------------------------------------------------------------------------------

    public static Byte[] ImportFile_ByteArray(string file)
    {
        var _path = CorrectPath(file);
        if (!File.Exists(_path)) return Array.Empty<byte>();
        return File.ReadAllBytes(_path);
    }
}
