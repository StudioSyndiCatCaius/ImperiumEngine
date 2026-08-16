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
        if (loaded != null) return loaded;
        if (string.IsNullOrEmpty(path)) return null;
        loaded = ImpAsset.Load<T>(path);
        return loaded;
    }

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty ui)
    {
        C2_Picker picker = new()
        {
            placeholder = "None",
            text_get = () =>
            {
                TRef<T> r = ui.Value_Get() is TRef<T> x ? x : default;
                return ImpAsset.Name_ForPath(r.path);
            },
            tint_get = () => ImpAsset.Color_ForType(typeof(T)),
            on_cleared = () => ui.Value_Set(new TRef<T>("")),
            drop_accepts = path => ImpAsset.Load(path) is T,
            on_dropped = path =>
            {
                if (ImpAsset.Load(path) is T)
                {
                    ui.Value_Set(new TRef<T>(path));
                }
            },
            on_open = () =>
            {
                TRef<T> r = ui.Value_Get() is TRef<T> x ? x : default;
                Dialog_AssetPicker.Run(typeof(T),
                    path => ui.Value_Set(new TRef<T>(path ?? "")),
                    current_path: r.path,
                    title: "Select " + C2_Tree.Class_DisplayName(typeof(T)));
            },
        };
        ui.Editor_Set(picker);
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
