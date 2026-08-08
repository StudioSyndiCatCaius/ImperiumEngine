using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3D_cs;
using Raylib_cs;
using Model = R3D_cs.Model;

namespace ImperiumEngine.Main;

// file that can be read/written from disc
public class ImpAsset
{
    // ==============================================================================================================
    // Statics
    // ==============================================================================================================
    private static readonly JsonSerializerOptions json_options = new()
    {
        WriteIndented = true,
        IncludeFields = true, //engine types (Color, Vector2, ...) expose fields, not properties
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonColorConverter() },
    };

    // resolved "_class" names, so type lookup only scans the assemblies once per name
    private static readonly Dictionary<string, Type?> class_cache = new();

    // Reads an .ImpAsset off disc. The file's "_class" picks the concrete type, so loading
    // as ImpAsset still gives back an A_Font when that's what the file says.
    // Note this does NOT import the source file: importing touches the GPU, so it is
    // deferred until first use via Resource_Ensure.
    public static T? Load<T>(string filepath) where T : ImpAsset
    {
        string full_path = ImpPath.Path_Resolve(filepath);
        if (!File.Exists(full_path)) return null;

        using var doc = JsonDocument.Parse(File.ReadAllText(full_path));
        var root = doc.RootElement;

        Type type = typeof(T);
        if (root.TryGetProperty("_class", out var cls) && cls.GetString() is string cls_name)
        {
            var found = Class_Find(cls_name);
            if (found != null && typeof(T).IsAssignableFrom(found)) type = found;
        }

        if (type.IsAbstract || Activator.CreateInstance(type) is not T asset) return null;

        asset.filepath = full_path;
        if (root.TryGetProperty("vars", out var vars)) asset.Vars_Read(vars);
        asset.OnLoaded();

        return asset;
    }

    static Type? Class_Find(string name)
    {
        if (class_cache.TryGetValue(name, out var cached)) return cached;

        Type? found = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var t in asm.GetTypes())
            {
                if (t.Name != name || !typeof(ImpAsset).IsAssignableFrom(t)) continue;
                found = t;
                break;
            }
            if (found != null) break;
        }

        class_cache[name] = found;
        return found;
    }

    // ==============================================================================================================
    // Class
    // ==============================================================================================================
    public string filepath="";
    public bool is_dirty = false;
    public uint resource_id = 0;

    //file this asset imports from, relative to the asset's own folder unless rooted
    [ImpVar] public string source_file="";

    // ---------------------------------------------------------
    // Resource
    // ---------------------------------------------------------

    public ImpResource? Resource_Get()
    {
        Resource_Ensure();
        ImpResource.files.TryGetValue(resource_id, out ImpResource? res);
        return res;
    }

    // Imports source_file on first use. Deferred rather than done at load because import
    // uploads to the GPU, which is only valid once the window exists.
    public void Resource_Ensure()
    {
        if (resource_id != 0 && ImpResource.files.ContainsKey(resource_id)) return;
        if (source_file == "") return;

        string full_path = ImpPath.Path_Resolve(source_file, filepath);

        //another asset may already have imported this exact file
        var res = ImpResource.Find(full_path);
        if (res == null)
        {
            res = Resource_Create(full_path);
            if (res == null) return;

            res._filepath = full_path;
            res.Import();
        }

        resource_id = res._id;
    }

    // Assets that import a source file override this to say what kind of resource it is.
    protected virtual ImpResource? Resource_Create(string full_path)
    {
        return null;
    }

    static T? Res_At<T>(List<T>? list, int num) where T : struct
    {
        return list != null && num >= 0 && num < list.Count ? list[num] : null;
    }

    public Image?     get_Image(int num=0)     { return Res_At(Resource_Get()?.r_image, num); }
    public Texture2D? get_Texture2D(int num=0) { return Res_At(Resource_Get()?.r_texture, num); }
    public Wave?      get_Wave(int num=0)      { return Res_At(Resource_Get()?.r_wave, num); }
    public Sound?     get_Sound(int num=0)     { return Res_At(Resource_Get()?.r_sound, num); }
    public Model?     get_Model(int num=0)     { return Res_At(Resource_Get()?.r_model, num); }
    public Skeleton?  get_Skeleton(int num=0)  { return Res_At(Resource_Get()?.r_skeleton, num); }
    public Font?      get_Font(int num=0)      { return Res_At(Resource_Get()?.r_font, num); }
    public FontType?  get_FontType(int num=0)  { return Res_At(Resource_Get()?.r_fonttype, num); }

    // ---------------------------------------------------------
    // File
    // ---------------------------------------------------------

    public void File_Save()
    {
        if (filepath == "") return;
        File_Write(filepath);
    }

    public void File_Load()
    {
        if (filepath == "") return;
        File_Read(filepath);
    }

    // On-disc shape is { "_class": "A_Font", "vars": { ... } }; _class lets Load pick the
    // concrete type, vars holds every [ImpVar] member.
    public void File_Write(string filePath)
    {
        var vars = new Dictionary<string, object?>();

        foreach (var member in GetType().GetMembers(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = member.GetCustomAttribute<ImpVarAttribute>();
            if (attr is null) continue;

            string key = attr.Name ?? member.Name;

            switch (member)
            {
                case FieldInfo field:
                    vars[key] = field.GetValue(this);
                    break;

                case PropertyInfo prop when prop.CanRead:
                    vars[key] = prop.GetValue(this);
                    break;
            }
        }

        var root = new Dictionary<string, object?>
        {
            ["_class"] = GetType().Name,
            ["vars"] = vars,
        };

        string json = JsonSerializer.Serialize(root, json_options);
        File.WriteAllText(ImpPath.Path_Resolve(filePath), json);
    }

    public void File_Read(string filePath)
    {
        string full_path = ImpPath.Path_Resolve(filePath);
        if (!File.Exists(full_path))
            return; // keep existing defaults

        using var doc = JsonDocument.Parse(File.ReadAllText(full_path));
        if (doc.RootElement.TryGetProperty("vars", out var vars)) Vars_Read(vars);
        OnLoaded();
    }

    void Vars_Read(JsonElement vars)
    {
        if (vars.ValueKind != JsonValueKind.Object) return;

        foreach (var member in GetType().GetMembers(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = member.GetCustomAttribute<ImpVarAttribute>();
            if (attr is null) continue;

            string key = attr.Name ?? member.Name;
            if (!vars.TryGetProperty(key, out JsonElement element))
                continue; // missing key → keep current value

            switch (member)
            {
                case FieldInfo field:
                {
                    object? value = JsonSerializer.Deserialize(element, field.FieldType, json_options);
                    if (value != null) field.SetValue(this, value);
                    break;
                }
                case PropertyInfo prop when prop.CanWrite:
                {
                    object? value = JsonSerializer.Deserialize(element, prop.PropertyType, json_options);
                    if (value != null) prop.SetValue(this, value);
                    break;
                }
            }
        }
    }

    public bool File_IsValid()
    {
        return File.Exists(filepath);
    }

    // -----------------
    // virtuals
    // -----------------

    // Runs once vars have been read, for assets that derive runtime state from them.
    public virtual void OnLoaded()
    {

    }

    public virtual string File_GetExtension()
    {
        return "ImpAsset";
    }
}
