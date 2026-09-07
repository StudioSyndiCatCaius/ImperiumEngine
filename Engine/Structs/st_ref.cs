using System.Text.Json.Serialization;

using Engine.Core;
using Engine.Globals;
using Engine.Interfaces;

namespace Engine.Structs;


// like TSoftObjectPtr in UE: a path string that resolves to an ImpAsset or ImpComp.
// Asset file: "{game}/Maps/Town.ImpScene"
// Inline unique asset: empty path + loaded instance (serialized as a nested table)
// Comp in a scene: "{game}/Maps/Town.ImpScene#A1B2C3D4E5F60708"
public struct TRef<T> : I_Property, IEquatable<TRef<T>> where T : class
{

    [ImpVar] public string path;
    
    T? loaded;

    public TRef(string path)
    {
        this.path = path ?? "";
        loaded = null;
    }

    public TRef(T? obj)
    {
        loaded = obj;
        path = PathOf(obj);
    }

    public T? Get()
    {
        // Path is the authority for file / builtin / scene-comp refs. Re-resolve every call so an
        // inspector path change (or JSON writing only `path`) cannot leave `loaded` pointing at
        // the old object. Empty path keeps `loaded` so an inline unique still works.
        if (string.IsNullOrEmpty(path))
            return loaded;

        if (typeof(ImpComp).IsAssignableFrom(typeof(T)))
            loaded = ResolveComp(path) as T;
        else
            loaded = GAsset.Asset_Load(path, typeof(T)) as T;
        return loaded;
    }

    public bool Equals(TRef<T> other)
    {
        if (!string.Equals(path ?? "", other.path ?? "", StringComparison.Ordinal))
            return false;
        if (!string.IsNullOrEmpty(path))
            return true;
        return ReferenceEquals(loaded, other.loaded);
    }

    public override bool Equals(object? obj) => obj is TRef<T> other && Equals(other);

    public override int GetHashCode() => (path ?? "").GetHashCode(StringComparison.Ordinal);

    public bool Property_IsCustomParse() => true;

    public void Property_Read(object value)
    {
        loaded = default;
        if (value is TTable tbl)
        {
            if (tbl.Has("path") && string.IsNullOrEmpty(tbl.get_String("type")))
            {
                path = tbl.get_String("path");
                return;
            }
            path = "";
            if (!typeof(ImpAsset).IsAssignableFrom(typeof(T)))
                return;
            Type inst_type = typeof(T);
            string type_name = tbl.get_String("type");
            if (!string.IsNullOrEmpty(type_name))
                inst_type = TClass<object>.Resolve(type_name) ?? inst_type;
            if (Activator.CreateInstance(inst_type) is ImpAsset inst)
            {
                inst.is_inlined = true;
                inst.Property_Read(tbl);
                loaded = inst as T;
            }
            return;
        }

        string s = value as string ?? "";
        if (s.Length > 1 && (s[0] == '$' || s[0] == '&' || s[0] == '!'))
        {
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s[(colon + 1)..];
        }
        path = s;
    }

    public object Property_Write()
    {
        if (string.IsNullOrEmpty(path) && loaded is ImpAsset a && (a.is_inlined || string.IsNullOrEmpty(a.filepath)))
            return a.Property_Write();
        if (string.IsNullOrEmpty(path) && loaded != null)
            return PathOf(loaded);
        return path ?? "";
    }

    static string PathOf(T? obj)
    {
        if (obj is ImpComp c)
        {
            if (c.id.IsNone) c.id = TGuid64.New();
            string scene_path = "";
            if (c.scene != null && !string.IsNullOrEmpty(c.scene.filepath))
                scene_path = GFile.Make_Path_Local(c.scene.filepath);
            return scene_path + "#" + c.id.ToString();
        }
        if (obj is ImpAsset a)
        {
            if (a.is_inlined || string.IsNullOrEmpty(a.filepath))
                return "";
            return a.filepath;
        }
        return "";
    }

    static ImpComp ResolveComp(string ref_path)
    {
        string scene_path = "";
        TGuid64 id = default;
        int sep = ref_path.LastIndexOf('#');
        if (sep >= 0)
        {
            scene_path = ref_path[..sep];
            id.Property_Read(ref_path[(sep + 1)..]);
        }
        else
        {
            sep = ref_path.LastIndexOf(':');
            if (sep < 0) return null;
            string suffix = ref_path[(sep + 1)..];
            if (suffix.Length == 0 || suffix.IndexOf('/') >= 0 || suffix.IndexOf('\\') >= 0)
                return null;
            id.Property_Read(suffix);
            if (id.IsNone) return null;
            scene_path = ref_path[..sep];
        }
        return ImpComp.Find(id, scene_path);
    }
}


// like TSubclassOf in UE
public struct TClass<T> : I_Property, IEquatable<TClass<T>>
{
    [ImpVar] public string class_name;

    Type? resolved;

    public TClass(string class_name)
    {
        this.class_name = class_name ?? "";
        resolved = null;
    }

    public TClass(Type? type)
    {
        class_name = type?.Name ?? "";
        resolved = type;
    }

    public bool Equals(TClass<T> other) =>
        string.Equals(class_name ?? "", other.class_name ?? "", StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is TClass<T> other && Equals(other);

    public override int GetHashCode() => (class_name ?? "").GetHashCode(StringComparison.Ordinal);

    public Type? Get()
    {
        if (resolved != null)
        {
            if (string.IsNullOrEmpty(class_name) || resolved.Name == class_name)
            {
                return resolved;
            }
        }
        resolved = Resolve(class_name);
        return resolved;
    }

    public static Type? Resolve(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        Type? best = null;
        foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type?[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            if (types == null)
            {
                continue;
            }
            for (int i = 0; i < types.Length; i++)
            {
                Type? t = types[i];
                if (t == null || t.Name != name)
                {
                    continue;
                }
                string ns = t.Namespace ?? "";
                if (ns.StartsWith("Imperium", StringComparison.Ordinal))
                {
                    return t;
                }
                if (best == null)
                {
                    best = t;
                }
            }
        }
        return best;
    }

    public ImpComp Spawn(ImpComp owner, Action<ImpComp> on_prespawn = null) //on_prespawn allows configuring vars BEFORE first update (as on before OnInit & OnBegin)
    {
        Type type = Get();
        if (owner == null || type == null || type.IsAbstract || !typeof(ImpComp).IsAssignableFrom(type))
        {
            return null;
        }
        ImpComp inst = Activator.CreateInstance(type) as ImpComp;
        if (inst == null)
        {
            return null;
        }
        inst.is_runtime = true;
        owner.Child_Add(inst);
        if (inst is Imp3D as3d)
        {
            on_prespawn?.Invoke(as3d);
        }
        return inst;
    }
    
    
}
