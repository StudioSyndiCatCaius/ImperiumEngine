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

    // True once this asset has a backing file — i.e. it is a reference, not an embedded
    // instance. Editor slot framing keys off this (blue = reference, red = instance).
    public bool is_reference => !string.IsNullOrEmpty(file_link);
    
    // -------------------------------------------------------------------------------------------------
    // File
    // -------------------------------------------------------------------------------------------------
    
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

        // the optional "_source" tag links back to the raw file this asset imports from (e.g. the
        // .png behind an A_Texture2D); several assets may wrap one source with different settings.
        if (table.TryGetValue(ImpToml.SourceKey, out object? src) && src is string sp)
            asset.file_source = ImpFile.Path_ToAbsolute(sp);

        return asset;
    }
    
    
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
        if (!string.IsNullOrEmpty(file_source))
            doc[ImpToml.SourceKey] = ImpFile.Path_ToRelative(file_source);
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

    public bool File_Delete()
    {
        return false;
    }
    
    //renames the linked file, technically the same as File_Move, but just changes the name, not full path.
    public bool File_Rename(string new_name)
    {
        return false;
    }
    
    //moves the linked file to a new location, updating the file_link and all asset references to it.
    public bool File_Move(string new_path)
    {
        return false;
    }
    
    // -------------------------------------------------------------------------------------------------
    // Editor
    // -------------------------------------------------------------------------------------------------

    public virtual void OnEditorWindow_Open() { }
    public virtual void OnEditorWindow_Close() { }
    
    // I_EditorAsset — the editor's content browser reads thumbnails through these.
    // Subclasses customize by overriding the protected Editor_* virtuals.
    public Texture2D GetThumbnailTexture() => Editor_GetThumbnailTexture();
    public Color GetThumbnailColor() => Editor_GetThumbnailColor();
    public string GetExtension() => Editor_GetExtension();

    virtual protected Texture2D Editor_GetThumbnailTexture() => new Texture2D(); //Id 0 = use the editor's default doc thumbnail
    virtual protected Color Editor_GetThumbnailColor() => Color.White;
    virtual protected string Editor_GetExtension() => ".impasset";

}
