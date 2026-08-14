using System.Text.Json.Serialization;
using ImperiumEngine.Comps._2D;
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
            options_build = () => C2_Picker.Options_Assets(typeof(T)),
            text_get = () =>
            {
                TRef<T> r = ui.Value_Get() is TRef<T> x ? x : default;
                return ImpAsset.Name_ForPath(r.path);
            },
            tint_get = () => ImpAsset.Color_ForType(typeof(T)),
            on_picked = opt => ui.Value_Set(new TRef<T>(opt.data as string ?? "")),
            on_cleared = () => ui.Value_Set(new TRef<T>("")),
            drop_accepts = path => ImpAsset.Load(path) is T,
            on_dropped = path =>
            {
                if (ImpAsset.Load(path) is T) ui.Value_Set(new TRef<T>(path));
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

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty ui)
    {
        C2_Picker picker = new()
        {
            placeholder = "None",
            options_build = () => C2_Picker.Options_Classes(typeof(T)),
            text_get = () => ui.Value_Get() is TClass<T> c ? c.class_name ?? "" : "",
            tint_get = () => ImpAsset.Color_ForType(typeof(T)),
            on_picked = opt => ui.Value_Set(new TClass<T>(opt.data as Type)),
            on_cleared = () => ui.Value_Set(new TClass<T>((Type)null)),
        };
        ui.Editor_Set(picker);
    }
}
