using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Files;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;

/// <summary>
/// Public static [ImpVar][Config] members, persisted as {project}/Config/{DeclaringType}.TOML.
/// Snapshot field initialisers first, then overlay the file so the inspector can revert.
/// </summary>
public static class ImpConfig
{
    static readonly List<Type> _categories = new();
    static readonly Dictionary<Type, List<MemberInfo>> _members = new();
    static readonly Dictionary<string, object> _defaults = new(StringComparer.Ordinal);
    static bool _scanned;

    public static IReadOnlyList<Type> Categories()
    {
        Scan();
        return _categories;
    }

    public static List<MemberInfo> Members(Type type)
    {
        Scan();
        if (type == null)
        {
            return new List<MemberInfo>();
        }
        if (_members.TryGetValue(type, out List<MemberInfo> list))
        {
            return list;
        }
        return new List<MemberInfo>();
    }

    public static bool Member_IsConfig(MemberInfo m)
    {
        if (m == null)
        {
            return false;
        }
        if (m.GetCustomAttribute<ImpVarAttribute>() == null)
        {
            return false;
        }
        if (m.GetCustomAttribute<ConfigAttribute>() == null)
        {
            return false;
        }
        if (m is FieldInfo f)
        {
            return f.IsPublic && f.IsStatic && !f.IsLiteral;
        }
        if (m is PropertyInfo p)
        {
            if (p.GetIndexParameters().Length != 0)
            {
                return false;
            }
            MethodInfo get = p.GetGetMethod();
            if (get == null || !get.IsPublic || !get.IsStatic)
            {
                return false;
            }
            return true;
        }
        return false;
    }

    public static bool Default_TryGet(Type type, string name, out object value)
    {
        Scan();
        if (type != null && !string.IsNullOrEmpty(name) && _defaults.TryGetValue(DefaultKey(type, name), out value))
        {
            return true;
        }
        value = null;
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
        return Path.Combine(root, "Config", type.Name + ".TOML");
    }

    public static void LoadAll()
    {
        Scan();
        for (int i = 0; i < _categories.Count; i++)
        {
            Load(_categories[i]);
        }
    }

    public static void SaveAll()
    {
        Scan();
        for (int i = 0; i < _categories.Count; i++)
        {
            Save(_categories[i]);
        }
    }

    public static void Load(Type type)
    {
        Scan();
        if (type == null || !_members.TryGetValue(type, out List<MemberInfo> members))
        {
            return;
        }
        string path = FilePath(type);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        File_TOML file = new() { filepath = path };
        if (!file.Parse())
        {
            return;
        }

        for (int i = 0; i < members.Count; i++)
        {
            MemberInfo m = members[i];
            string key = KeyOf(m);
            if (!file.doc.root.values.TryGetValue(key, out object raw))
            {
                continue;
            }
            Type mt = MemberType(m);
            if (mt == null)
            {
                continue;
            }
            try
            {
                object val = FromToml(raw, mt);
                Member_Set(m, val);
            }
            catch (Exception e)
            {
                Console.WriteLine("ImpConfig.Load " + type.Name + "." + m.Name + ": " + e.Message);
            }
        }
    }

    public static void Save(Type type)
    {
        Scan();
        if (type == null || !_members.TryGetValue(type, out List<MemberInfo> members))
        {
            return;
        }
        string path = FilePath(type);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        TomlDoc doc = new();
        for (int i = 0; i < members.Count; i++)
        {
            MemberInfo m = members[i];
            object val;
            try
            {
                val = Member_Get(m);
            }
            catch
            {
                continue;
            }
            doc.root.Set(KeyOf(m), ToToml(val));
        }

        File_TOML file = new() { filepath = path, doc = doc };
        if (!file.WriteDoc())
        {
            Console.WriteLine("ImpConfig.Save failed: " + path);
        }
    }

    public static string CategoryName(Type type)
    {
        if (type == null)
        {
            return "";
        }
        TitleAttribute title = type.GetCustomAttribute<TitleAttribute>();
        if (title != null && !string.IsNullOrEmpty(title.Name))
        {
            return title.Name;
        }
        return type.Name;
    }

    static void Scan()
    {
        if (_scanned)
        {
            return;
        }
        _scanned = true;
        _categories.Clear();
        _members.Clear();

        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            string an = asm.GetName().Name;
            if (an != null && an.Equals("Editor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            if (types == null)
            {
                continue;
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t == null || t.IsGenericTypeDefinition)
                {
                    continue;
                }

                List<MemberInfo> found = new();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
                FieldInfo[] fields = t.GetFields(flags);
                for (int f = 0; f < fields.Length; f++)
                {
                    if (Member_IsConfig(fields[f]))
                    {
                        found.Add(fields[f]);
                    }
                }
                PropertyInfo[] props = t.GetProperties(flags);
                for (int p = 0; p < props.Length; p++)
                {
                    if (Member_IsConfig(props[p]))
                    {
                        found.Add(props[p]);
                    }
                }
                if (found.Count == 0)
                {
                    continue;
                }

                _members[t] = found;
                _categories.Add(t);
                for (int m = 0; m < found.Count; m++)
                {
                    Snapshot(found[m]);
                }
            }
        }

        _categories.Sort((a, b) => string.Compare(CategoryName(a), CategoryName(b), StringComparison.OrdinalIgnoreCase));
    }

    static void Snapshot(MemberInfo m)
    {
        Type decl = m.DeclaringType;
        if (decl == null)
        {
            return;
        }
        string key = DefaultKey(decl, m.Name);
        if (_defaults.ContainsKey(key))
        {
            return;
        }
        try
        {
            _defaults[key] = Member_Get(m);
        }
        catch
        {
        }
    }

    static string DefaultKey(Type type, string name)
    {
        string tn = type.FullName;
        if (string.IsNullOrEmpty(tn))
        {
            tn = type.Name;
        }
        return tn + "." + name;
    }

    static string KeyOf(MemberInfo m)
    {
        ConfigAttribute cfg = m.GetCustomAttribute<ConfigAttribute>();
        if (cfg != null && !string.IsNullOrEmpty(cfg.Name))
        {
            return cfg.Name;
        }
        ImpVarAttribute iv = m.GetCustomAttribute<ImpVarAttribute>();
        if (iv != null && !string.IsNullOrEmpty(iv.Name))
        {
            return iv.Name;
        }
        return m.Name;
    }

    static Type MemberType(MemberInfo m)
    {
        if (m is FieldInfo f)
        {
            return f.FieldType;
        }
        if (m is PropertyInfo p)
        {
            return p.PropertyType;
        }
        return null;
    }

    static object Member_Get(MemberInfo m)
    {
        if (m is FieldInfo f)
        {
            return f.GetValue(null);
        }
        if (m is PropertyInfo p)
        {
            return p.GetValue(null);
        }
        return null;
    }

    static void Member_Set(MemberInfo m, object value)
    {
        if (m is FieldInfo f)
        {
            f.SetValue(null, value);
            return;
        }
        if (m is PropertyInfo p && p.CanWrite)
        {
            p.SetValue(null, value);
        }
    }

    static object ToToml(object value)
    {
        if (value == null)
        {
            return "";
        }
        if (value is bool || value is string || value is int || value is long || value is float || value is double
            || value is short || value is byte || value is uint || value is ulong)
        {
            return value;
        }
        if (value is Enum)
        {
            return value.ToString();
        }
        if (value is Vector2 v2)
        {
            return new float[] { v2.X, v2.Y };
        }
        if (value is Vector3 v3)
        {
            return new float[] { v3.X, v3.Y, v3.Z };
        }
        if (value is Vector4 v4)
        {
            return new float[] { v4.X, v4.Y, v4.Z, v4.W };
        }
        if (value is Color c)
        {
            return new float[] { c.R, c.G, c.B, c.A };
        }

        Type t = value.GetType();
        if (t.IsGenericType)
        {
            Type gen = t.GetGenericTypeDefinition();
            if (gen == typeof(TRef<>))
            {
                FieldInfo path = t.GetField("path");
                string p = "";
                if (path != null)
                {
                    p = path.GetValue(value) as string ?? "";
                }
                return Path_Store(p);
            }
            if (gen == typeof(TClass<>))
            {
                FieldInfo cn = t.GetField("class_name");
                if (cn != null)
                {
                    return cn.GetValue(value) as string ?? "";
                }
                return "";
            }
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    static object FromToml(object raw, Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string))
        {
            if (raw == null)
            {
                return "";
            }
            return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
        }
        if (type == typeof(bool))
        {
            if (raw is bool b)
            {
                return b;
            }
            string s = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
        if (type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
            || type == typeof(short) || type == typeof(byte) || type == typeof(float) || type == typeof(double)
            || type == typeof(decimal))
        {
            return Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }
        if (type.IsEnum)
        {
            string name = Convert.ToString(raw, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(name) && Enum.TryParse(type, name, true, out object ev))
            {
                return ev;
            }
            return Activator.CreateInstance(type);
        }
        if (type == typeof(Vector2))
        {
            float[] a = AsFloats(raw);
            if (a != null && a.Length >= 2)
            {
                return new Vector2(a[0], a[1]);
            }
            return Vector2.Zero;
        }
        if (type == typeof(Vector3))
        {
            float[] a = AsFloats(raw);
            if (a != null && a.Length >= 3)
            {
                return new Vector3(a[0], a[1], a[2]);
            }
            return Vector3.Zero;
        }
        if (type == typeof(Vector4))
        {
            float[] a = AsFloats(raw);
            if (a != null && a.Length >= 4)
            {
                return new Vector4(a[0], a[1], a[2], a[3]);
            }
            return Vector4.Zero;
        }
        if (type == typeof(Color))
        {
            float[] a = AsFloats(raw);
            if (a != null && a.Length >= 3)
            {
                byte r = (byte)Math.Clamp(a[0], 0, 255);
                byte g = (byte)Math.Clamp(a[1], 0, 255);
                byte bl = (byte)Math.Clamp(a[2], 0, 255);
                byte al = 255;
                if (a.Length >= 4)
                {
                    al = (byte)Math.Clamp(a[3], 0, 255);
                }
                return new Color(r, g, bl, al);
            }
            return new Color(255, 255, 255, 255);
        }
        if (type.IsGenericType)
        {
            Type gen = type.GetGenericTypeDefinition();
            if (gen == typeof(TRef<>) || gen == typeof(TClass<>))
            {
                string s = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
                if (gen == typeof(TRef<>))
                {
                    s = Path_Load(s);
                }
                try
                {
                    return Activator.CreateInstance(type, s);
                }
                catch
                {
                    return Activator.CreateInstance(type);
                }
            }
        }
        return raw;
    }

    static float[] AsFloats(object raw)
    {
        if (raw is float[] fa)
        {
            return fa;
        }
        if (raw is IList list)
        {
            float[] arr = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    arr[i] = Convert.ToSingle(list[i], CultureInfo.InvariantCulture);
                }
                catch
                {
                    arr[i] = 0;
                }
            }
            return arr;
        }
        return null;
    }

    static string Path_Store(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }
        if (path.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace('\\', '/');
        }
        if (path.Contains("{engine}") || path.Contains("{game}"))
        {
            return path.Replace('\\', '/');
        }
        return File_JSON.Path_Tokenize(path);
    }

    static string Path_Load(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }
        if (path.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }
        return File_JSON.Path_ResolveRef(null, path);
    }
}
