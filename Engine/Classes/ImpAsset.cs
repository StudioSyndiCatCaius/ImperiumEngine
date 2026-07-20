using ImperiumEngine.Interfaces;
using Raylib_cs;
using Tomlyn;
using Tomlyn.Model;

namespace ImperiumEngine.Classes;

//An asset that can be save & loaded to and from disk. (.impasset). Similar to Resources in Godot
public class ImpAsset : I_EditorAsset
{
    public string file_link = "";   // the filepath to the asset on disk
    public string file_source = ""; // the filepath to the asset this will use if importing data (e.g texture, 3d model, sound, etc)
    public bool is_dirty=false;   // true if the asset has been modified since it was last saved

    // Set once at startup by the engine entry point
    public static string s_projectDir = "";
    public static string s_engineContentDir = "";

    
    
    /*
     * Resolve path keywords to absolute paths:
     *      {engine} / {Engine} : the Engine "Content" directory
     *      {editor} / {Editor} : the Engine "Content" directory
     *      {game}              : the Game/Project's "Content" directory.
     *          NOTE: if `CFG_Modding.modding_enabled` is true, will load from the game's Mod directory by matching file path
     *          Mod files are ONLY loaded at runtime, NOT editor. Editor will always use native game Content assets.
     */
    public static string ResolvePath(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        return source
            .Replace("{game}",   Path.Combine(s_projectDir, "Content"))
            .Replace("{engine}", s_engineContentDir)
            .Replace("{Engine}", s_engineContentDir)
            .Replace("{editor}", s_engineContentDir)
            .Replace("{Editor}", s_engineContentDir);
    }

    // Inverse of ResolvePath: rewrites an absolute path back into keyword form so stored
    // references stay portable across machines. Unmatched paths are returned normalized.
    public static string ToKeywordPath(string absolute)
    {
        if (string.IsNullOrEmpty(absolute)) return absolute;
        string norm   = absolute.Replace('\\', '/');
        string game   = Path.Combine(s_projectDir, "Content").Replace('\\', '/');
        string engine = s_engineContentDir.Replace('\\', '/');

        if (!string.IsNullOrEmpty(game) && norm.StartsWith(game, StringComparison.OrdinalIgnoreCase))
            return "{game}" + norm[game.Length..];
        if (!string.IsNullOrEmpty(engine) && norm.StartsWith(engine, StringComparison.OrdinalIgnoreCase))
            return "{engine}" + norm[engine.Length..];
        return norm;
    }

    // True once this asset has a backing file — i.e. it is a reference, not an embedded
    // instance. Editor slot framing keys off this (blue = reference, red = instance).
    public bool IsReference => !string.IsNullOrEmpty(file_link);

    // Reads a standalone .impasset TOML file, instantiating the concrete type named by its
    // "_type" tag (or `expected` when the file omits one). The result carries file_link,
    // so it is treated as a reference. Returns null if the file is missing or unreadable.
    public static ImpAsset? LoadFile(string filepath, Type? expected = null)
    {
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath)) return null;

        TomlTable table;
        try { table = Toml.ToModel(File.ReadAllText(filepath)); }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImpAsset] Could not read {filepath}: {ex.Message}");
            return null;
        }

        var type = ImpToml.ResolveAssetType(table, expected ?? typeof(ImpAsset));
        if (type == null) return null;

        var asset = (ImpAsset)Activator.CreateInstance(type)!;
        ImpToml.ReadParams(asset, table);
        asset.file_link = filepath;   // marks it as a reference
        return asset;
    }

    // I_EditorAsset — the editor's content browser reads thumbnails through these.
    // Subclasses customize by overriding the protected Editor_* virtuals.
    public Texture2D GetThumbnailTexture() => Editor_GetThumbnailTexture();
    public Color GetThumbnailColor() => Editor_GetThumbnailColor();
    public string GetExtension() => Editor_GetExtension();

    virtual protected Texture2D Editor_GetThumbnailTexture() => new Texture2D(); //Id 0 = use the editor's default doc thumbnail
    virtual protected Color Editor_GetThumbnailColor() => Color.White;
    virtual protected string Editor_GetExtension() => ".impasset";

    public virtual bool File_Load(string filepath)
    {
        file_link = filepath;
        return false;
    }
    
    // Writes this asset's [ImpVar] params (only those differing from defaults) to a
    // standalone .impasset TOML file, tagged with its concrete "_type". Subclasses that
    // import external data (textures, meshes...) may override for their own format.
    public virtual bool File_Save(string filepath)
    {
        var doc = new TomlTable { [ImpToml.TypeKey] = GetType().Name };
        ImpToml.WriteParams(this, Activator.CreateInstance(GetType()), doc);

        try { File.WriteAllText(filepath, Toml.FromModel(doc)); }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImpAsset] Could not write {filepath}: {ex.Message}");
            return false;
        }

        file_link = filepath;
        is_dirty = false;
        return true;
    }
}
