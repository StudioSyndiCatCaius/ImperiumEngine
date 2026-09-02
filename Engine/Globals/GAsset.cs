using System.Reflection;
using Engine.Core;
using Engine.Structs;

namespace Engine.Globals;

public struct TBuiltin
{
    public string key;
    public string name;
    public ImpAsset asset;
}

public static class GAsset
{
    const string BUILTIN_PREFIX = "{builtin}/";
    static bool builtins_ready;
    static readonly List<TBuiltin> builtins = new();
    static readonly Dictionary<string, ImpAsset> builtins_by_key = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<ImpAsset> builtins_set = new();

    public static IReadOnlyList<TBuiltin> Builtin_All()
    {
        Builtin_Ensure();
        return builtins;
    }

    public static bool Builtin_Is(ImpAsset? asset)
    {
        if (asset == null) return false;
        Builtin_Ensure();
        return builtins_set.Contains(asset);
    }
    
    public static T? Asset_Load<T>(string filepath) where T : ImpAsset
    {
        return Asset_Load(filepath, typeof(T)) as T;
    }
    
    public static ImpAsset Asset_Load(string filepath, Type type = null)
    {
        TFile pth = new(filepath);
        if (App.assets.ContainsKey(pth)) return App.assets[pth];

        Builtin_Ensure();
        if (App.assets.ContainsKey(pth)) return App.assets[pth];
        if (!string.IsNullOrEmpty(filepath) && builtins_by_key.TryGetValue(filepath, out ImpAsset builtin))
            return builtin;

        if (!string.IsNullOrEmpty(filepath) &&
            (filepath.StartsWith(BUILTIN_PREFIX, StringComparison.OrdinalIgnoreCase) ||
             filepath.StartsWith("{builtin}\\", StringComparison.OrdinalIgnoreCase)))
        {
            GLog.Error($"Could not find asset {filepath}");
            return null;
        }

        string abs = GFile.Make_Path_Absolute(filepath);
        if (string.IsNullOrEmpty(filepath) || !File.Exists(abs))
        {
            GLog.Error($"Could not find asset {filepath}");
            return null;
        }

        TTable _tbl = TTable.FromTOML(GFile.LoadAs_String(filepath));
        Type _type = TClass<object>.Resolve(_tbl.get_String("type")) ?? type ?? typeof(ImpAsset);
        ImpAsset asset = Activator.CreateInstance(_type) as ImpAsset;
        if (asset == null) return null;
        App.assets.Add(pth, asset);
        asset.filepath = filepath;
        asset.From_Table(_tbl);
        asset.Source_Reimport();
        return asset;
    }
    
    //imports an ImpFile fromt a sourcefile and then creates OR loads an ImpAsset from the sourcefile.
    public static T? Asset_Import<T>(string sourcefile) where T : ImpAsset
    {
        Console.WriteLine(" --------------  ");
        if (string.IsNullOrEmpty(sourcefile)) return null;

        string abs_src = GFile.Make_Path_Absolute(sourcefile);
        if (!File.Exists(abs_src))
        {
            GLog.Error($"Could not find sourcefile {sourcefile}");
            return null;
        }

        ImpFile file = GFile.Import<ImpFile>(sourcefile);
        if (file == null) return null;

        T asset = Activator.CreateInstance<T>();
        if (asset == null) return null;

        string asset_path = Path.ChangeExtension(sourcefile, asset.GetFileExtension());
        TFile pth = new(asset_path);
        if (App.assets.ContainsKey(pth)) return App.assets[pth] as T;

        if (File.Exists(GFile.Make_Path_Absolute(asset_path)))
            return Asset_Load<T>(asset_path);

        asset.sourcefile = file.filepath;
        asset.filepath = asset_path;
        App.assets[pth] = asset;
        asset.Source_Reimport();
        return asset;
    }
    
    static void Builtin_Ensure()
    {
        if (builtins_ready) return;
        builtins_ready = true;

        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            foreach (Type t in types)
            {
                if (t == null || t.IsGenericTypeDefinition) continue;
                if ((t.Namespace ?? "").StartsWith("Editor_Vibe")) continue;

                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                    Collect(t, f, f.FieldType, () => f.GetValue(null));
                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    Collect(t, p, p.PropertyType, () => p.GetValue(null));
                }
            }
        }

        builtins.Sort((a, b) =>
        {
            int c = string.Compare(a.asset.GetType().Name, b.asset.GetType().Name, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });

        void Collect(Type owner, MemberInfo member, Type member_type, Func<object?> get)
        {
            if (!typeof(ImpAsset).IsAssignableFrom(member_type)) return;
            BuiltinAttribute? attr = member.GetCustomAttribute<BuiltinAttribute>();
            if (attr == null) return;
            ImpAsset? asset;
            try { asset = get() as ImpAsset; }
            catch { return; }
            if (asset == null) return;

            string key = attr.Name;
            if (string.IsNullOrEmpty(key)) key = owner.Name + "." + member.Name;
            if (builtins_by_key.ContainsKey(key))
            {
                GLog.Error($"Duplicate builtin '{key}' on {owner.Name}.{member.Name}");
                return;
            }

            if (string.IsNullOrEmpty(asset.filepath))
                asset.filepath = BUILTIN_PREFIX + key;

            string name = string.IsNullOrEmpty(attr.Name) ? member.Name : attr.Name;
            TBuiltin entry = new() { key = key, name = name, asset = asset };
            builtins.Add(entry);
            builtins_set.Add(asset);
            builtins_by_key[key] = asset;
            builtins_by_key[asset.filepath] = asset;
            App.assets[new TFile(asset.filepath)] = asset;
            App.assets[new TFile(key)] = asset;
        }
    }
}
