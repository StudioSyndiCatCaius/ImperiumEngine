using ImperiumCore.Const;
using ImperiumEngine.Interfaces;

namespace ImperiumCore.Classes;

public class ImpAsset : ImpObject, I_Saveable
{
    // id under which this asset lives in the app registry (null if unregistered)
    public string? RegistryId { get; private set; }

    /*
     * optional and not every asset will have a source file. Used for things like textures, meshes, sound, etc.
     * a saved ImpAsset with a source file will look something like this:
     *      - T_Pingo.png               - source file
     *      - T_Pingo.ImpAsset          - ImpAsset file
     */
    public string? SourceFile { get; set; }

    // When true, this asset has been saved to its own file at serialPath.
    // ImpSave uses these two flags to decide how to write an ImpAsset [ImpVar] field:
    //   serialized = true  → store the path string (external reference)
    //   serialized = false → embed all [ImpVar] fields as an inline sub-table
    public bool    serialized { get; set; }
    public string? serialPath { get; set; }
    
    // ----------------------------------------------------------------------------------------------------
    // STATICS
    // ----------------------------------------------------------------------------------------------------

    //Add a new asset to the asset registry (found in ImpApp)
    static public bool Register(ImpAsset asset, string id, bool Override=false)
    {
        var _app = ImpApp.Active;
        if (_app == null || asset == null || string.IsNullOrEmpty(id)) return false;
        if (_app.AssetRegistry.ContainsKey(id) && !Override) return false;

        asset.RegistryId = id;
        _app.AssetRegistry[id] = asset;
        return true;
    }

    //fetch an already-registered asset by its id
    static public T? Load<T>(string id) where T : ImpAsset
    {
        var _app = ImpApp.Active;
        if (_app != null && _app.AssetRegistry.TryGetValue(id, out var _asset) && _asset is T _typed)
            return _typed;
        Console.WriteLine($"Asset '{id}' not found in registry.");
        return null;
    }

    static public Task<T?> Load_Async<T>(string id) where T : ImpAsset
        => Task.FromResult(Load<T>(id));

    // When importing a new asset with "AddToRegistry", the registry id will be the same as the filename with NO extension
    // E.G. "D:/Images/T_Pingo.png" id would be "T_Pingo" (This MAY change longterm)
    // Per-type import logic lives in each asset's Import_Try override (e.g. A_Texture2D loads pixels there).

    static public T? Import<T>(string path, bool AddToRegistry=false) where T : ImpAsset, new()
    {
        var _resolved = ImpFile.CorrectPath(path);


        var _asset = new T();
        if (!_asset.Import_Try(_resolved)) return null;
        _asset.SourceFile = _resolved;

        if (AddToRegistry)
        {
            string _id = Path.GetFileNameWithoutExtension(_resolved);
            Console.WriteLine($"Registering asset '{_id}'");
            Register(_asset, _id);
        }

        return _asset;
    }

    static public Task<T?> Import_Async<T>(string path, bool AddToRegistry=false) where T : ImpAsset, new()
        => Task.Run(() => Import<T>(path, AddToRegistry));

    // ----------------------------------------------------------------------------------------------------
    // IMPLEMENTATION
    // ----------------------------------------------------------------------------------------------------

    //reruns the import logic for this asset from its SourceFile.
    public bool Reimport()
    {
        if(string.IsNullOrWhiteSpace(SourceFile)) return false;
        return Import_Try(SourceFile);
    }
    
    public string GetSaveExtension()
    {
        return ImpC_String.EXT_ASSET;
    }

    //what happens when trying to import an asset?
    public virtual bool Import_Try(string filepath)
    {
        return false;
    }
    
    public void Savable_OnRead(string path)  => ImpSave.Read(this, path);
    public void Savable_OnWrite(string path) => ImpSave.Write(this, path);
}

