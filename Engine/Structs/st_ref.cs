using System.Text.Json.Serialization;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Dialogs;
using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;


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
        path = asset?.filepath ?? "";
        loaded = asset;
    }

    public T? Get()
    {
        // Path is the authority for file / builtin refs. Re-resolve every call so an inspector
        // path change (or JSON writing only `path`) cannot leave `loaded` pointing at the old asset.
        // Empty path keeps `loaded` so an inline unique (C2_AssetSlot clone) still works.
        if (!string.IsNullOrEmpty(path))
        {
            T resolved = ImpAsset.Load<T>(path);
            if (resolved != null)
            {
                loaded = resolved;
            }
            return loaded;
        }
        return loaded;
    }

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty ui)
    {
        C2_AssetSlot slot = new()
        {
            name = ui.name,
            label = ui.label,
            asset_type = typeof(T),
            value_get = () =>
            {
                TRef<T> r = ui.Value_Get() is TRef<T> x ? x : default;
                return r.Get();
            },
            value_set = a =>
            {
                T asset = a as T;
                ui.Value_Set(new TRef<T>(asset));
            },
            rows_build = ui.Depth_CanNest ? ui.Rows_ForObject : null,
        };
        ui.Editor_SetFull(slot);
    }
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

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty ui)
    {
        C2_Picker picker = new()
        {
            placeholder = "None",
            text_get = () => ui.Value_Get() is TClass<T> c ? c.class_name ?? "" : "",
            tint_get = () => ImpAsset.Color_ForType(typeof(T)),
            on_cleared = () => ui.Value_Set(new TClass<T>((Type)null)),
            on_open = () =>
            {
                TClass<T> c = ui.Value_Get() is TClass<T> x ? x : default;
                Dialog_ClassPicker.Run(typeof(T),
                    t => ui.Value_Set(new TClass<T>(t)),
                    title: "Select " + C2_Tree.Class_DisplayName(typeof(T)),
                    current: c.Get(),
                    allow_none: true);
            },
        };
        ui.Editor_Set(picker);
    }
}
