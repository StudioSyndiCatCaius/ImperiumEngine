using System.Reflection;
using System.Text.RegularExpressions;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Files;
using ImperiumEngine.Interfaces;
using Raylib_cs;

namespace ImperiumEngine;

public class ImpAsset : I_File, I_Property
{
    // #################################################################################
    // Static
    // #################################################################################

    static readonly Dictionary<string, ImpAsset> _loaded = new(StringComparer.OrdinalIgnoreCase);

    static string CacheKey(string path)
    {
        string real = ImpFile.Path_Resolve(path);
        try { return Path.GetFullPath(real); }
        catch { return real; }
    }

    /// <summary>
    /// Repoints cached assets after a file or folder moved on disk. Takes both the exact path
    /// and anything underneath it, so moving a folder carries its contents.
    /// </summary>
    public static void Cache_Rekey(string old_path, string new_path)
    {
        if (string.IsNullOrEmpty(old_path) || string.IsNullOrEmpty(new_path)) return;
        string from, to;
        try
        {
            from = Path.GetFullPath(old_path);
            to = Path.GetFullPath(new_path);
        }
        catch { return; }

        List<string> keys = new(_loaded.Keys);
        foreach (string key in keys)
        {
            bool exact = key.Equals(from, StringComparison.OrdinalIgnoreCase);
            bool under = key.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!exact && !under) continue;

            ImpAsset asset = _loaded[key];
            _loaded.Remove(key);
            string moved = exact ? to : to + key[from.Length..];
            asset.filepath = moved;
            _loaded[moved] = asset;
        }
    }

    /// <summary>Drops cached assets at this path and anything underneath it after a delete.</summary>
    public static void Cache_Drop(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        string from;
        try { from = Path.GetFullPath(path); }
        catch { return; }

        List<string> keys = new(_loaded.Keys);
        foreach (string key in keys)
        {
            bool exact = key.Equals(from, StringComparison.OrdinalIgnoreCase);
            bool under = key.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            if (!exact && !under) continue;
            _loaded.Remove(key);
        }
    }

    public static Type? AssetType_FromName(string name)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type?[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            foreach (Type? t in types)
            {
                if (t == null || t.Name != name || t.IsAbstract) continue;
                if (!typeof(ImpAsset).IsAssignableFrom(t)) continue;
                return t;
            }
        }
        return null;
    }

    // Engine built-ins (A_Mesh.GEO_CUBE, A_Font.FONT_ARIAL, ...) live as static members with no
    // file behind them. They are addressed as "builtin:<Type>.<Member>" so they save and load
    // through the same string path as file assets.
    public const string BuiltinPrefix = "builtin:";

    static Dictionary<string, ImpAsset>? _builtins;

    public static bool Path_IsBuiltin(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith(BuiltinPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Display name for an asset path, builtin or file.</summary>
    public static string Name_ForPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        if (Path_IsBuiltin(path))
        {
            string k = path[BuiltinPrefix.Length..];
            int dot = k.LastIndexOf('.');
            return dot >= 0 ? k[(dot + 1)..] : k;
        }
        try { return Path.GetFileNameWithoutExtension(path); }
        catch { return path; }
    }

    public static Dictionary<string, ImpAsset> Builtins_All()
    {
        if (_builtins != null) return _builtins;
        _builtins = new Dictionary<string, ImpAsset>(StringComparer.OrdinalIgnoreCase);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type?[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            foreach (Type? t in types)
            {
                if (t == null || !typeof(ImpAsset).IsAssignableFrom(t)) continue;
                foreach (MemberInfo m in t.GetMembers(flags))
                {
                    Type? mt = m switch
                    {
                        FieldInfo f => f.FieldType,
                        PropertyInfo p when p.CanRead && p.GetIndexParameters().Length == 0 => p.PropertyType,
                        _ => null,
                    };
                    if (mt == null || !typeof(ImpAsset).IsAssignableFrom(mt)) continue;

                    object? val;
                    try { val = m is FieldInfo fi ? fi.GetValue(null) : ((PropertyInfo)m).GetValue(null); }
                    catch { continue; }
                    if (val is not ImpAsset asset) continue;

                    string key = t.Name + "." + m.Name;
                    if (_builtins.ContainsKey(key)) continue;
                    // A_Game.GAME_TEST (and similar) already point at a real project file
                    // via gamepath. Stamping builtin: here made GetRootDir forget the project.
                    if (string.IsNullOrEmpty(asset.filepath) && asset is not A_Game)
                    {
                        asset.filepath = BuiltinPrefix + key;
                    }
                    _builtins[key] = asset;
                }
            }
        }
        return _builtins;
    }

    public static ImpAsset? Builtin_Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (Path_IsBuiltin(key)) key = key[BuiltinPrefix.Length..];
        return Builtins_All().GetValueOrDefault(key);
    }

    public static List<(string key, ImpAsset asset)> Builtins_OfType(Type type)
    {
        List<(string, ImpAsset)> found = new();
        if (type == null) return found;
        foreach (var kv in Builtins_All())
            if (type.IsAssignableFrom(kv.Value.GetType())) found.Add((kv.Key, kv.Value));
        return found;
    }

    public static ImpAsset? Load(string filepath)
    {
        if (string.IsNullOrWhiteSpace(filepath)) return null;
        if (Path_IsBuiltin(filepath)) return Builtin_Get(filepath);
        string key = CacheKey(filepath);
        if (_loaded.TryGetValue(key, out ImpAsset? cached)) return cached;

        string real = ImpFile.Path_Resolve(filepath);
        if (!File.Exists(real)) return null;

        File_JSON peek = new() { filepath = real };
        if (!peek.Parse()) return null;
        string? class_name = peek.ClassName;
        if (string.IsNullOrEmpty(class_name)) return null;
        Type? type = AssetType_FromName(class_name);
        if (type == null) return null;

        if (Activator.CreateInstance(type) is not ImpAsset asset) return null;
        asset.filepath = filepath;
        _loaded[key] = asset;
        if (!asset.File_Read())
        {
            _loaded.Remove(key);
            return null;
        }
        return asset;
    }

    public static T? Load<T>(string filepath) where T : ImpAsset
    {
        return Load(filepath) as T;
    }

    public static T? Import<T>(string filepath) where T : ImpAsset, new()
    {
        if (string.IsNullOrWhiteSpace(filepath)) return null;
        string realpath = ImpFile.Path_Resolve(filepath);
        if (ImpFile.Create_FromExtension(realpath) == null) return null;

        ImpFile file = ImpFile.GetOrCreate(realpath);
        file.Reimport();

        T asset = new T();
        asset.source_file = file;
        asset.Source_OnReload(file);
        return asset;
    }

    // #################################################################################
    // Class
    // #################################################################################
    public string filepath; // path to the saved .ImpAsset — empty if imported and never saved
    public ImpFile? source_file;
    public int source_index;
    public bool is_dirty;

    public ImpAsset(string filepath = "")
    {
        this.filepath = filepath ?? "";
    }

    public string GetName()
    {
        if (File_IsValid())
        {
            return Name_ForPath(filepath);
        }
        return "Untitled*";
    }

    // ------------------------------------
    // Source
    // ------------------------------------

    [CallInEditor]
    public void Source_Reimport()
    {
        BindSource(true);
    }

    public virtual void Source_OnReload(ImpFile file) { }

    internal void BindSource(bool force)
    {
        if (source_file == null || string.IsNullOrEmpty(source_file.filepath)) return;
        source_file = ImpFile.GetOrCreate(source_file.filepath);
        source_file.Reimport(force);
        Source_OnReload(source_file);
    }

    // ------------------------------------
    // File
    // ------------------------------------

    public bool File_IsValid()
    {
        if (string.IsNullOrEmpty(filepath)) return false;
        if (Path_IsBuiltin(filepath)) return true;
        return File.Exists(ImpFile.Path_Resolve(filepath));
    }

    // Has a real disk path we can write. Builtins and never-saved untitled assets cannot.
    public bool File_CanWrite()
    {
        if (string.IsNullOrWhiteSpace(filepath)) return false;
        if (Path_IsBuiltin(filepath)) return false;
        return true;
    }

    ImpFile? File_CreateParser()
    {
        if (Activator.CreateInstance(File_GetParser()) is not ImpFile parser) return null;
        parser.filepath = ImpFile.Path_Resolve(filepath);
        return parser;
    }

    public virtual bool File_Read()
    {
        if (string.IsNullOrWhiteSpace(filepath) || Path_IsBuiltin(filepath)) return false;
        ImpFile? parser = File_CreateParser();
        if (parser == null || !parser.File_Read(this)) return false;
        BindSource(false);

        string key = CacheKey(filepath);
        if (!_loaded.ContainsKey(key)) _loaded[key] = this;
        is_dirty = false;
        return true;
    }

    public virtual bool File_Write()
    {
        if (!File_CanWrite()) return false;
        ImpFile? parser = File_CreateParser();
        if (parser == null || !parser.File_Write(this)) return false;
        is_dirty = false;
        _loaded[CacheKey(filepath)] = this;
        return true;
    }

    // Point this asset at a new path, write it, and rebind the load cache.
    public bool File_SaveTo(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path_IsBuiltin(path)) return false;
        string dest = ImpFile.Path_Resolve(path);
        try { dest = Path.GetFullPath(dest); }
        catch { }

        if (File_CanWrite())
        {
            string old_key = CacheKey(filepath);
            if (_loaded.TryGetValue(old_key, out ImpAsset? cached) && cached == this)
            {
                _loaded.Remove(old_key);
            }
        }

        filepath = dest;
        return File_Write();
    }

    public virtual string File_GetExtension() { return "ImpAsset"; }

    public virtual Type File_GetParser() { return typeof(File_JSON); }

    // ------------------------------------
    // Property
    // ------------------------------------

    public virtual bool Inspector_IsCustom() { return true; }

    public virtual void Inspector_Rebuild(C2_InspectorProperty ui)
    {
        C2_AssetSlot slot = new()
        {
            name = ui.name,
            label = ui.label,
            asset_type = ui.value_type,
            value_get = () => ui.Value_Get() as ImpAsset,
            value_set = a => ui.Value_Set(a),
            rows_build = ui.Depth_CanNest ? ui.Rows_ForObject : null,
        };
        ui.Editor_SetFull(slot);
    }

    public static List<(string path, string cls)> Files_OfType(Type type)
    {
        List<(string, string)> found = new();
        // The game and engine roots collapse to the same folder when no project is loaded.
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string root in new[] { ImpFile.ContentDir_Game(), ImpFile.ContentDir_Engine() })
        {
            if (!Directory.Exists(root)) continue;
            string[] files;
            try { files = Directory.GetFiles(root, "*.Imp*", SearchOption.AllDirectories); }
            catch { continue; }
            foreach (string file in files)
            {
                string full;
                try { full = Path.GetFullPath(file); }
                catch { full = file; }
                if (!seen.Add(full)) continue;

                File_JSON peek = new() { filepath = file };
                if (!peek.Parse()) continue;
                string? cls = peek.ClassName;
                if (string.IsNullOrEmpty(cls)) continue;
                Type? resolved = AssetType_FromName(cls);
                if (resolved == null || !type.IsAssignableFrom(resolved)) continue;
                found.Add((file, cls));
            }
        }
        return found;
    }

    public static Color Color_ForType(Type type)
    {
        if (type == null) return new Color((byte)90, (byte)90, (byte)90, (byte)255);
        for (Type t = type; t != null; t = t.BaseType)
        {
            AssetColorAttribute attr = t.GetCustomAttribute<AssetColorAttribute>(false);
            if (attr == null) continue;
            return new Color(attr.R, attr.G, attr.B, (byte)255);
        }
        return new Color((byte)90, (byte)90, (byte)90, (byte)255);
    }

    public virtual ImpAsset Clone()
    {
        if (Activator.CreateInstance(GetType()) is not ImpAsset copy) return null;
        foreach (FieldInfo f in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.IsInitOnly || f.IsLiteral) continue;
            f.SetValue(copy, f.GetValue(this));
        }
        copy.filepath = "";
        copy.is_dirty = true;
        return copy;
    }

    // ------------------------------------
    // Editor File
    // ------------------------------------

    public static Action<ImpAsset> Editor_OnOpenAsset;

    public static void SaveAllDirty()
    {
        foreach (ImpAsset asset in _loaded.Values)
        {
            if (asset != null && asset.is_dirty && asset.File_IsValid())
                asset.File_Write();
        }
    }

    public static IEnumerable<ImpAsset> Loaded_GetAll() => _loaded.Values;

    public override void Editor_File_Open()
    {
        Editor_OnOpenAsset?.Invoke(this);
    }

    public override List<TPopupMenuOption> Editor_File_GetOptions()
    {
        return new List<TPopupMenuOption>
        {
            new() { text = "Open", on_press = Editor_File_Open },
            new() { text = "Save", on_press = () => File_Write(), is_disabled = !is_dirty || !File_IsValid() },
            new() { text = "Show in Explorer", on_press = () => Editor_ShowInExplorer() },
        };
    }

    public override Color Editor_GetThumbnail_Color()
    {
        AssetColorAttribute attr = GetType().GetCustomAttribute<AssetColorAttribute>();
        if (attr != null) return new Color(attr.R, attr.G, attr.B, (byte)255);
        return new Color((byte)90, (byte)90, (byte)90, (byte)255);
    }

    public override Texture2D? Editor_GetThumbnail_Texture()
    {
        return null;
    }

    public virtual string Editor_GetTypeLabel()
    {
        string n = GetType().Name;
        if (n.StartsWith("A_")) n = n[2..];
        if (n.StartsWith("Imp")) n = n[3..];
        return Regex.Replace(n, "([a-z])([A-Z])", "$1 $2");
    }

    public void Editor_ShowInExplorer()
    {
        if (!File_IsValid()) return;
        string real = ImpFile.Path_Resolve(filepath);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + real + "\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }
    
    
    // ------------------------------------
    // Scene View
    // ------------------------------------
    public virtual void SceneDrop_Enter(Imp2D view, ImpPlayer player) { }
    public virtual void SceneDrop_Exit(Imp2D view, ImpPlayer player) { }
    public virtual void SceneDrop_Update(Imp2D view, float dt, ImpPlayer player) { }
    
    public virtual void SceneDrop_CompEnter(ImpComp comp, ImpPlayer player) { }
    public virtual void SceneDrop_CompExit(ImpComp comp, ImpPlayer player) { }
    public virtual ImpComp SceneDrop_DropOnComp(ImpComp comp, ImpPlayer player) { return null; }
}
