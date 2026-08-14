using System.Data;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Interfaces;
using Raylib_cs;

namespace ImperiumEngine;

public enum EFileType
{
    Texture, Sound, Model, Animation, Font,
}

public class ImpFile : I_File
{
    // #################################################################################
    // Static
    // #################################################################################

    const string FileTypeNamespace = "ImperiumEngine.Files";
    
    public static string ContentDir_Engine()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "Content");
    }
    
    public static string ContentDir_Game()
    {
        try
        {
            string? dir = A_Game.game?.GetRootDir();
            if (string.IsNullOrWhiteSpace(dir))
                return Path.Combine(Directory.GetCurrentDirectory(), "Content");
            return Path.Combine(dir, "Content");
        }
        catch
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Content");
        }
    }
    
    public static string Path_Resolve(string path)
    {
        string output=path;
        output=output.Replace("{engine}", ContentDir_Engine());
        output=output.Replace("{game}", ContentDir_Game());
        return output;
    }

    static readonly Dictionary<string, ImpFile> _loaded = new(StringComparer.OrdinalIgnoreCase);

    static string CacheKey(string path)
    {
        string real = Path_Resolve(path);
        try { return Path.GetFullPath(real); }
        catch { return real; }
    }

    /// <summary>Repoints cached source files after a file or folder moved on disk.</summary>
    public static void Cache_Rekey(string old_path, string new_path)
    {
        if (string.IsNullOrEmpty(old_path) || string.IsNullOrEmpty(new_path)) return;
        string from, to;
        try
        {
            from = Path.GetFullPath(old_path);
            to = Path.GetFullPath(new_path);
        }
        catch { return; }

        List<string> keys = new(_loaded.Keys);
        foreach (string key in keys)
        {
            bool exact = key.Equals(from, StringComparison.OrdinalIgnoreCase);
            bool under = key.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!exact && !under) continue;

            ImpFile file = _loaded[key];
            _loaded.Remove(key);
            string moved = exact ? to : to + key[from.Length..];
            file.filepath = moved;
            _loaded[moved] = file;
        }
    }

    /// <summary>Cached ImpFile for a source path. Parser from extension, or a generic ImpFile if none exists.</summary>
    public static ImpFile GetOrCreate(string filepath)
    {
        string real = Path_Resolve(filepath);
        string key = CacheKey(real);
        if (_loaded.TryGetValue(key, out ImpFile? existing)) return existing;

        ImpFile file = Create_FromExtension(real) ?? new ImpFile();
        file.filepath = real;
        _loaded[key] = file;
        return file;
    }

    /// <summary>Pick an ImpFile importer by name: .png → File_PNG (class under ImperiumEngine.Files).</summary>
    public static ImpFile? Create_FromExtension(string filepath)
    {
        string ext = Path.GetExtension(filepath);
        if (string.IsNullOrEmpty(ext)) return null;

        // File_ + extension without dot (File_PNG, File_JSON, …)
        // jpg/jpeg are textures — reuse File_PNG (Raylib LoadTexture handles both)
        string extKey = ext.TrimStart('.').ToUpperInvariant();
        if (extKey is "JPG" or "JPEG") extKey = "PNG";
        string typeName = "File_" + extKey;

        Type? type =
            Type.GetType($"{FileTypeNamespace}.{typeName}")
            ?? typeof(ImpFile).Assembly.GetType($"{FileTypeNamespace}.{typeName}");

        // fallback: match type name anywhere in loaded assemblies (case-insensitive)
        if (type == null)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetTypes().FirstOrDefault(t =>
                        t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                        && typeof(ImpFile).IsAssignableFrom(t)
                        && !t.IsAbstract);
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }
                if (type != null) break;
            }
        }

        if (type == null) return null;
        return Activator.CreateInstance(type) as ImpFile;
    }
    
    
    // #################################################################################
    // Class
    // #################################################################################
    public string filepath;
    public EFileType file_type;
    public Type default_asset_type;
    public DataTable parsed_data = new();
    
    public List<Texture2D> src_textures = new();
    public List<Sound> src_sounds = new();
    public List<Model> src_models = new();
    public List<Font> src_fonts = new();

    public void Reimport(bool force = false)
    {
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath)) return;
        if (!force && (src_textures.Count > 0 || src_sounds.Count > 0 || src_models.Count > 0 || src_fonts.Count > 0))
            return;

        Invalidate();
        switch (file_type)
        {
            case EFileType.Texture:
                Texture2D _txt=Raylib.LoadTexture(filepath);
                src_textures.Add(_txt);
                break;
            case EFileType.Sound:
                Sound _snd=Raylib.LoadSound(filepath);
                src_sounds.Add(_snd);
                break;
            case EFileType.Model:
                Model _mdl=Raylib.LoadModel(filepath);
                src_models.Add(_mdl);
                break;
            case EFileType.Animation:
                break;
            case EFileType.Font:
                Font _fnt=Raylib.LoadFont(filepath);
                src_fonts.Add(_fnt);
                break;
            default:
                break;
        }
    }
    
    public void Invalidate()
    {
        parsed_data?.Clear();
        src_textures.Clear();
        src_sounds.Clear();
        src_models.Clear();
        src_fonts.Clear();
    }

    public virtual bool File_Read(object target) { return false; }
    public virtual bool File_Write(object target) { return false; }

    
    // -----------------------------------------
    // Editor File
    // -----------------------------------------
    public override void Editor_File_Open()
    {
        base.Editor_File_Open();
    }

    public override List<TPopupMenuOption> Editor_File_GetOptions()
    {
        List<TPopupMenuOption> options = new();
        options.Add(new() { text = "Source_Reimport" , on_press = () => Reimport(true) });
        options.Add(new() { text = "Create Asset" , on_press = () => Editor_CreateAsset() });

        return options;
    }

    public override Color Editor_GetThumbnail_Color()
    {
        return Color.Gray;
    }

    public override Texture2D? Editor_GetThumbnail_Texture()
    {
        if (file_type == EFileType.Texture)
        {
            Reimport();
            if (src_textures.Count > 0) return src_textures[0];
        }
        return base.Editor_GetThumbnail_Texture();
    }

    public void Editor_CreateAsset()
    {
        if (default_asset_type == null || string.IsNullOrEmpty(filepath)) return;
        if (Activator.CreateInstance(default_asset_type) is not ImpAsset asset) return;
        string dir = Path.GetDirectoryName(filepath) ?? "";
        string name = Path.GetFileNameWithoutExtension(filepath);
        string dest = Path.Combine(dir, name + "." + asset.File_GetExtension());
        if (File.Exists(dest))
        {
            int i = 1;
            while (File.Exists(Path.Combine(dir, name + "_" + i + "." + asset.File_GetExtension()))) i++;
            dest = Path.Combine(dir, name + "_" + i + "." + asset.File_GetExtension());
        }
        asset.source_file = this;
        asset.filepath = dest;
        Reimport();
        asset.Source_OnReload(this);
        asset.File_Write();
    }
}