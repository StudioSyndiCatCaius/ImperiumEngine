using Engine.Core;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Globals;

public static class GFile
{
    // =================================================================================================================
    // PATH
    // =================================================================================================================

    public static string Path_Normalize(string path)
    {
        string _pth = path;
        return _pth
                .Replace("//","/")
                .Replace("/","\\")
                .Replace("\\\\","\\")
            ;
    }

    public static string Path_Ascend(string directory, int dir_number)
    {
        if (string.IsNullOrEmpty(directory) || dir_number <= 0)
        {
            return directory;
        }

        DirectoryInfo currentDir = new DirectoryInfo(directory);
        
        for (int i = 0; i < dir_number; i++)
        {
            if (currentDir.Parent != null)
            {
                currentDir = currentDir.Parent;
            }
            else
            {
                break; // Stop if we reach the root directory
            }
        }

        return currentDir.FullName;
    }

    public static string GetDir_Root(EContentDir dir)
    {
        switch (dir)
        {
            case EContentDir.Game:
                return GetFileDir(App.game_file);
            case EContentDir.Engine:
                Console.WriteLine("ENIGNE ROOT IS:  "+AppContext.BaseDirectory);
#if DEBUG
                return Path_Ascend(AppContext.BaseDirectory,4);
#else
                return AppContext.BaseDirectory;
#endif
        }
        return "";
    }

    public static string GetDir_Content(EContentDir dir)
    {
        switch (dir)
        {
            case EContentDir.Game:
                return GetDir_Root(EContentDir.Game) + "\\Content\\";
            case EContentDir.Engine:
                return GetDir_Root(EContentDir.Engine) + "\\Content\\";
        }
        return "";
    }

    public static string Make_Path_Absolute(string path)
    {
        string output = Path_Normalize(path)
            .Replace("{game}", GetDir_Content(EContentDir.Game))
            .Replace("{engine}", GetDir_Content(EContentDir.Engine));
        return output;
    }

    public static string Make_Path_Local(string path)
    {
        string output = Path_Normalize(path)
            .Replace(GetDir_Content(EContentDir.Game),"{game}")
            .Replace(GetDir_Content(EContentDir.Engine),"{engine}");
        return output;
        
    }

    public static string GetExePath() { return Path_Normalize(System.Environment.ProcessPath); }

    public static string GetFileDir(string filepath)
    {
        string _path = "";
        var dirnam = Path.GetDirectoryName(filepath);
        if (dirnam != null) _path = Path_Normalize(dirnam);
        return _path;
    }

    // =================================================================================================================
    // FILE
    // =================================================================================================================

    public static string GetFirstOfExt(string dir, string extension)
    {
        if (!Directory.Exists(dir))
            return "";

        extension = extension.TrimStart('.');

        return Directory
            .GetFiles(dir, $"*.{extension}", SearchOption.TopDirectoryOnly)
            .FirstOrDefault() ?? "";
    }

    public static string LoadAs_String(string filepath)
    {
        string _path = Make_Path_Absolute(filepath);
        return File.ReadAllText(_path);
    }

    public static TByteBlock LoadAs_Bytes(string filepath) { return new (){ data = File.ReadAllBytes(Make_Path_Absolute(filepath))}; }

    public static Type GetType_FromExtension(string file)
    {
        string _ext = Path.GetExtension(file).TrimStart('.').ToUpper();
        return Type.GetType("Engine.Files.File_" + _ext);
    }

    private static ImpFile _ImportInternal(string filepath, bool force = false, Type? type = null)
    {
        TFile pth = new(filepath);
        if (!force && App.files.TryGetValue(pth, out ImpFile existing)) return existing;

        type ??= GetType_FromExtension(filepath) ?? typeof(ImpFile);
        ImpFile _new_file = Activator.CreateInstance(type) as ImpFile;
        if (_new_file == null) return null;
        _new_file.filepath = filepath;
        _new_file.Reimport();
        App.files[pth] = _new_file;
        return _new_file;
    }

    public static T? Import<T>(string filepath, bool force=false) where T : ImpFile
    {
        Type type = typeof(T);
        if (type == typeof(ImpFile)) type = GetType_FromExtension(filepath) ?? typeof(ImpFile);
        return _ImportInternal(filepath, force, type) as T;
    }

    public static void Import_AllInDir(string dir, bool force = false)
    {
        foreach (string file in Directory.GetFiles(dir))
        {
            Type? _type = GetType_FromExtension(file);
            if (_type != null)
            {
                _ImportInternal(file, force, _type);
            }
        }
    }

    // =================================================================================================================
    // MOD
    // =================================================================================================================

    public static string Mod_GetDirRoot(string mod_name)
    {
        
        return "";
    }
    

    public static string Mod_GetFilePath(string mod_name, string file_name)
    {
        string _root= Mod_GetDirRoot(mod_name);
        string _ext = Path.GetExtension(file_name);
        
        string full_path = _root + "/"+ _ext + "/" + file_name;
        return full_path;
    }
}
