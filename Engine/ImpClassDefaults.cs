using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Files;

namespace ImperiumEngine;

/// <summary>
/// Per-class instance [ImpVar][Config] overlays written by WND_ConfigComp.
/// Static [Config] vars live in ImpConfig / Game Config instead.
/// Applied on <c>new()</c> / <see cref="ImpComp.Create"/> so spawned comps pick them up.
/// Scene load and Clone skip the overlay so already-placed comps stay as authored.
/// </summary>
public static class ImpClassDefaults
{
    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    static readonly Dictionary<Type, JsonObject> _overlays = new();
    static readonly Dictionary<Type, ImpComp> _prototypes = new();
    static readonly Dictionary<Type, ImpComp> _code = new();
    static int _skip;

    public static void Skip_Begin()
    {
        _skip++;
    }

    public static void Skip_End()
    {
        if (_skip > 0)
        {
            _skip--;
        }
    }

    public static bool IsHidden(Type type)
    {
        for (Type n = type; n != null; n = n.BaseType)
        {
            ImpClassAttribute attr = n.GetCustomAttribute<ImpClassAttribute>(false);
            if (attr != null && attr.Hidden)
            {
                return true;
            }
        }
        return false;
    }

    public static bool Member_IsConfig(MemberInfo m)
    {
        if (m == null)
        {
            return false;
        }
        ImpVarAttribute iv = m.GetCustomAttribute<ImpVarAttribute>();
        if (iv == null || iv.Hidden)
        {
            return false;
        }
        if (m.GetCustomAttribute<ConfigAttribute>() == null)
        {
            return false;
        }
        Type mt = null;
        if (m is FieldInfo f)
        {
            if (!f.IsPublic || f.IsStatic || f.IsLiteral)
            {
                return false;
            }
            mt = f.FieldType;
        }
        else if (m is PropertyInfo p)
        {
            if (p.GetIndexParameters().Length != 0)
            {
                return false;
            }
            MethodInfo get = p.GetGetMethod();
            if (get == null || !get.IsPublic || get.IsStatic)
            {
                return false;
            }
            mt = p.PropertyType;
        }
        else
        {
            return false;
        }
        if (m.Name == "name")
        {
            return false;
        }
        if (mt != null && typeof(ImpComp).IsAssignableFrom(mt))
        {
            return false;
        }
        return true;
    }

    public static bool Type_HasConfig(Type type)
    {
        if (type == null || type.IsGenericTypeDefinition)
        {
            return false;
        }
        if (!typeof(ImpComp).IsAssignableFrom(type))
        {
            return false;
        }
        if (IsHidden(type))
        {
            return false;
        }
        List<MemberInfo> members = C2_Inspector.Members_Get(type);
        for (int i = 0; i < members.Count; i++)
        {
            if (Member_IsConfig(members[i]))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsPrototype(object obj)
    {
        if (obj is not ImpComp inst)
        {
            return false;
        }
        Type t = inst.GetType();
        if (_prototypes.TryGetValue(t, out ImpComp p) && ReferenceEquals(p, inst))
        {
            return true;
        }
        return false;
    }

    public static string FilePath(Type type)
    {
        if (type == null)
        {
            return "";
        }
        string root = A_Game.game?.GetRootDir();
        if (string.IsNullOrWhiteSpace(root))
        {
            return "";
        }
        return Path.Combine(root, "Config", "Comps", type.Name + ".json");
    }

    public static void LoadAll()
    {
        _overlays.Clear();
        _prototypes.Clear();
        _code.Clear();
        C2_Inspector.Default_Invalidate();

        string root = A_Game.game?.GetRootDir();
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }
        string dir = Path.Combine(root, "Config", "Comps");
        if (!Directory.Exists(dir))
        {
            return;
        }

        string[] files = Directory.GetFiles(dir, "*.json");
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            JsonObject parsed = null;
            try
            {
                parsed = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            }
            catch (Exception e)
            {
                Console.WriteLine("ImpClassDefaults.Load " + path + ": " + e.Message);
            }
            if (parsed == null)
            {
                continue;
            }

            string cls = Path.GetFileNameWithoutExtension(path);
            if (parsed["_class"] is JsonValue jn && jn.TryGetValue<string>(out string named) && !string.IsNullOrEmpty(named))
            {
                cls = named;
            }
            Type type = ImpComp.Type_FromName(cls);
            if (type == null || !Type_HasConfig(type))
            {
                continue;
            }

            JsonObject vars = parsed["vars"] as JsonObject;
            if (vars == null)
            {
                vars = parsed;
            }
            if (vars.Count == 0)
            {
                continue;
            }
            _overlays[type] = vars;
        }
    }

    public static void SaveAll()
    {
        HashSet<Type> types = new();
        foreach (Type t in _overlays.Keys)
        {
            types.Add(t);
        }
        foreach (Type t in _prototypes.Keys)
        {
            types.Add(t);
        }
        foreach (Type t in types)
        {
            Save(t);
        }
    }

    public static void Save(Type type)
    {
        if (type == null || type.IsAbstract || IsHidden(type) || !Type_HasConfig(type))
        {
            return;
        }
        ImpComp proto = Prototype(type);
        ImpComp code = CodeDefault(type);
        if (proto == null || code == null)
        {
            return;
        }

        JsonObject diff = Diff(File_JSON.ImpVars_ToJson(proto), File_JSON.ImpVars_ToJson(code));
        string path = FilePath(type);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (diff.Count == 0)
        {
            _overlays.Remove(type);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ImpClassDefaults.Save delete " + path + ": " + e.Message);
                }
            }
            C2_Inspector.Default_Invalidate(type);
            return;
        }

        _overlays[type] = diff;
        JsonObject node = new()
        {
            ["_class"] = type.Name,
            ["vars"] = diff.DeepClone(),
        };
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, node.ToJsonString(WriteOptions));
        }
        catch (Exception e)
        {
            Console.WriteLine("ImpClassDefaults.Save " + path + ": " + e.Message);
        }
        C2_Inspector.Default_Invalidate(type);
    }

    public static void Apply(ImpComp inst)
    {
        if (inst == null || _skip > 0)
        {
            return;
        }
        Type type = inst.GetType();
        if (IsHidden(type))
        {
            return;
        }
        if (!_overlays.TryGetValue(type, out JsonObject vars) || vars == null)
        {
            return;
        }
        File_JSON.ImpVars_Apply(inst, vars, FilePath(type));
    }

    public static ImpComp Prototype(Type type)
    {
        if (type == null || type.IsAbstract)
        {
            return null;
        }
        if (_prototypes.TryGetValue(type, out ImpComp hit) && hit != null)
        {
            return hit;
        }
        ImpComp inst = ImpComp.CreateBare(type);
        if (inst == null)
        {
            return null;
        }
        if (_overlays.TryGetValue(type, out JsonObject vars) && vars != null)
        {
            File_JSON.ImpVars_Apply(inst, vars, FilePath(type));
        }
        _prototypes[type] = inst;
        return inst;
    }

    public static ImpComp CodeDefault(Type type)
    {
        if (type == null || type.IsAbstract)
        {
            return null;
        }
        if (_code.TryGetValue(type, out ImpComp hit) && hit != null)
        {
            return hit;
        }
        ImpComp inst = ImpComp.CreateBare(type);
        if (inst == null)
        {
            return null;
        }
        _code[type] = inst;
        return inst;
    }

    static JsonObject Diff(JsonObject proto, JsonObject code)
    {
        JsonObject diff = new();
        if (proto == null)
        {
            return diff;
        }
        foreach (var kv in proto)
        {
            JsonNode other = null;
            if (code != null)
            {
                code.TryGetPropertyValue(kv.Key, out other);
            }
            string a = kv.Value?.ToJsonString() ?? "null";
            string b = other?.ToJsonString() ?? "null";
            if (a == b)
            {
                continue;
            }
            if (kv.Value == null)
            {
                diff[kv.Key] = null;
            }
            else
            {
                diff[kv.Key] = kv.Value.DeepClone();
            }
        }
        return diff;
    }
}
