using System.Text.Json.Serialization;

using Engine.Core;
using Engine.Interfaces;

namespace Engine.Structs;


// like TSoftObjectPtr in UE
public struct TRef<T> : I_Property where T : ImpAsset
{

    [ImpVar] public string path;
    
    T? loaded;

    public TRef(string path)
    {
        this.path = path ?? "";
        loaded = null;
    }

    public TRef(T? asset)
    {
        //path = asset?.filepath ?? "";
        loaded = asset;
    }

    public T? Get()
    {
        // Path is the authority for file / builtin refs. Re-resolve every call so an inspector
        // path change (or JSON writing only `path`) cannot leave `loaded` pointing at the old asset.
        // Empty path keeps `loaded` so an inline unique (C2_AssetSlot clone) still works.
        if (!string.IsNullOrEmpty(path))
        {
            //T resolved = ImpAsset.Asset_Load<T>(path);
            //if (resolved != null)
            //{
           //     loaded = resolved;
          //  }
            return loaded;
        }
        return loaded;
    }

    public bool Inspector_IsCustom() => true;

 
}


// like TSubclassOf in UE
public struct TClass<T> : I_Property
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

}
