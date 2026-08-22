using System.Data;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Interfaces;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine;

public enum EFileType
{
    Texture, Sound, Model, Animation, Font,
}

// One asset that can be written from an ImpFile (a named mesh, texture, clip, …).
public class TFileAssetOffer
{
    public Type asset_type;
    public string name;
    public int source_index;
}

public class ImpFile : I_File
{
    // #################################################################################
    // Static
    // #################################################################################

    const string FileTypeNamespace = "ImperiumEngine.Files";
    
    static string _engine_content;

    // Dev: Engine/Content next to the .csproj. Built: Content beside the exe.
    public static string ContentDir_Engine()
    {
        if (!string.IsNullOrEmpty(_engine_content))
        {
            return _engine_content;
        }
        string found = EngineContent_Find(AppContext.BaseDirectory);
        if (string.IsNullOrEmpty(found))
        {
            found = EngineContent_Find(Directory.GetCurrentDirectory());
        }
        if (string.IsNullOrEmpty(found))
        {
            string beside = Path.Combine(AppContext.BaseDirectory, "Content");
            if (Directory.Exists(beside))
            {
                found = beside;
            }
        }
        if (string.IsNullOrEmpty(found))
        {
            found = Path.Combine(AppContext.BaseDirectory, "Content");
        }
        try
        {
            _engine_content = Path.GetFullPath(found);
        }
        catch
        {
            _engine_content = found;
        }
        return _engine_content;
    }

    static string EngineContent_Find(string start)
    {
        if (string.IsNullOrEmpty(start))
        {
            return null;
        }
        string dir;
        try
        {
            dir = Path.GetFullPath(start);
        }
        catch
        {
            return null;
        }
        for (int i = 0; i < 8; i++)
        {
            string nested = Path.Combine(dir, "Engine", "Content");
            if (Directory.Exists(nested))
            {
                return nested;
            }
            if (File.Exists(Path.Combine(dir, "Engine.csproj")))
            {
                string here = Path.Combine(dir, "Content");
                if (Directory.Exists(here))
                {
                    return here;
                }
            }
            DirectoryInfo parent = Directory.GetParent(dir);
            if (parent == null)
            {
                break;
            }
            dir = parent.FullName;
        }
        return null;
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

    /// <summary>Drops cached source files at this path and anything underneath it after a delete.</summary>
    public static void Cache_Drop(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        string from;
        try { from = Path.GetFullPath(path); }
        catch { return; }

        List<string> keys = new(_loaded.Keys);
        foreach (string key in keys)
        {
            bool exact = key.Equals(from, StringComparison.OrdinalIgnoreCase);
            bool under = key.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!exact && !under) continue;
            _loaded.Remove(key);
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
        if (extKey is "EXR") extKey = "HDR";
        if (extKey is "GLTF" or "FBX" or "OBJ") extKey = "GLB";
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
    public List<R3D_cs.Model> src_models = new();
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
                src_models.Add(R3D.LoadModelEx(filepath, ImportFlags.RetainMeshNames));
                if (src_models.Count > 0)
                {
                    Textures_CollectFromModel(src_models[0]);
                }
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

    protected void Textures_CollectFromModel(R3D_cs.Model mdl)
    {
        R3D_cs.Material def = R3D.GetDefaultMaterial();
        HashSet<uint> seen = new();

        void Skip(Texture2D tex)
        {
            if (tex.Id != 0)
            {
                seen.Add(tex.Id);
            }
        }

        void Take(Texture2D tex)
        {
            if (tex.Id == 0)
            {
                return;
            }
            if (seen.Contains(tex.Id))
            {
                return;
            }
            seen.Add(tex.Id);
            src_textures.Add(tex);
        }

        Skip(def.Albedo.Texture);
        Skip(def.Orm.Texture);
        Skip(def.Normal.Texture);
        Skip(def.Emission.Texture);

        Span<R3D_cs.Material> mats = mdl.Materials;
        for (int i = 0; i < mats.Length; i++)
        {
            Take(mats[i].Albedo.Texture);
            Take(mats[i].Orm.Texture);
            Take(mats[i].Normal.Texture);
            Take(mats[i].Emission.Texture);
        }
    }

    
    // -----------------------------------------
    // Editor File
    // -----------------------------------------

    // Editor wires this to DLG_CreateAssetFromFile. Null (standalone) writes the default asset.
    public static Action<ImpFile> Editor_OnCreateAssetFromFile;

    public override void Editor_File_Open()
    {
        base.Editor_File_Open();
    }

    public override List<TPopupMenuOption> Editor_File_GetOptions()
    {
        List<TPopupMenuOption> options = new();
        options.Add(new() { text = "Source_Reimport" , on_press = () => Reimport(true) });
        if (default_asset_type != null)
        {
            options.Add(new()
            {
                text = "Create Asset",
                on_press = () =>
                {
                    if (Editor_OnCreateAssetFromFile != null)
                    {
                        Editor_OnCreateAssetFromFile(this);
                    }
                    else
                    {
                        Editor_CreateAsset();
                    }
                }
            });
        }

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

    public virtual List<TFileAssetOffer> Editor_ListCreateableAssets()
    {
        List<TFileAssetOffer> list = new();
        if (default_asset_type == null || string.IsNullOrEmpty(filepath))
        {
            return list;
        }
        Reimport();
        string name = Path.GetFileNameWithoutExtension(filepath);
        if (string.IsNullOrEmpty(name))
        {
            name = "Asset";
        }
        list.Add(new TFileAssetOffer
        {
            asset_type = default_asset_type,
            name = name,
            source_index = 0,
        });
        return list;
    }

    public static string AssetName_Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }
        name = name.Trim();
        char[] bad = Path.GetInvalidFileNameChars();
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(bad, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }
        return new string(chars).Trim('_', ' ');
    }

    // Silent default used by Import Sources. Writes the default-type offer next to the file.
    public void Editor_CreateAsset()
    {
        List<TFileAssetOffer> offers = Editor_ListCreateableAssets();
        if (offers.Count == 0)
        {
            return;
        }
        TFileAssetOffer pick = offers[0];
        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i].asset_type == default_asset_type)
            {
                pick = offers[i];
                break;
            }
        }
        Editor_WriteAsset(pick.asset_type, pick.name, pick.source_index);
    }

    public ImpAsset Editor_WriteAsset(Type type, string name, int source_index)
    {
        if (type == null || type.IsAbstract || !typeof(ImpAsset).IsAssignableFrom(type))
        {
            return null;
        }
        if (string.IsNullOrEmpty(filepath))
        {
            return null;
        }
        if (Activator.CreateInstance(type) is not ImpAsset asset)
        {
            return null;
        }

        string dir = Path.GetDirectoryName(filepath) ?? "";
        name = (name ?? "").Trim();
        string ext = "." + asset.File_GetExtension();
        if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - ext.Length);
        }
        name = AssetName_Sanitize(name);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        string dest = Path.Combine(dir, name + ext);
        if (File.Exists(dest))
        {
            int i = 1;
            while (File.Exists(Path.Combine(dir, name + "_" + i + ext)))
            {
                i++;
            }
            dest = Path.Combine(dir, name + "_" + i + ext);
        }

        asset.source_file = this;
        asset.source_index = source_index;
        asset.filepath = dest;
        Reimport();
        asset.Source_OnReload(this);
        if (!asset.File_Write())
        {
            return null;
        }
        return asset;
    }
}