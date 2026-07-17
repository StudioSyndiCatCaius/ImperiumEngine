using System.Collections;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using ImperiumCore.Structs;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;
using Tomlyn;
using Tomlyn.Model;

namespace ImperiumCore.Classes;

// TOML (de)serializer for any object's [ImpVar] members (fields AND properties).
//
// Write: only members whose value differs from a fresh default are stored.
// Read:  members absent from the file keep their existing value (their default).
//
// ImpAsset [ImpVar] fields:
//   asset.serialized = true  → stored as a path string (external reference; loaded from that file on read)
//   asset.serialized = false → embedded as an inline sub-table with __type
//
// Struct/object [ImpVar] fields:
//   If the type implements I_PropertyType, Savable_ToToml/Savable_FromToml are called first.
//   Otherwise, if the type itself has [ImpVar] members they are recursed into as a sub-table.
public class ImpSave
{
    [ImpVar] public TGlobalVars Vars;
    [ImpVar] public TCreatureSet creatures;
    [ImpVar] public DateTime time_created;
    [ImpVar] public DateTime time_lastSaved;
    
    // ----------------------------------------------------------------------------------------------------
    // STATICS
    // ----------------------------------------------------------------------------------------------------

    
    // -------------------------------------------------------
    // Save game
    // -------------------------------------------------------
    public static ImpSave_Game Game_Create() //creates a new save game
    {
        return new ImpSave_Game();
    }

    public static bool Game_Start(ImpSave_Game save, bool loadSavedLevel) //loads a save game
    {
        return true;
    }
    
    public static bool Game_Save(int slot) // saves the current game to the specified slot
    {
        return true;
    }
    
    // -------------------------------------------------------
    // Save global
    // -------------------------------------------------------

    public static bool Global_Reload(bool createIfMissing) //loads the global save file, creating it if missing
    {
        return true;
    }
    
    public static bool Global_Save()
    {
        return true;
    }
    
    // ################################################################################################################
    // SERIALIZATION - should eventually be moved out, probably into ImpParse
    // ################################################################################################################
    
    private static readonly TomlModelOptions _opts = new()
    {
        IncludeFields = true,
        IgnoreMissingProperties = true,
        ConvertPropertyName = _n => _n,
        ConvertFieldName    = _n => _n,
    };

    // ----------------------------------------------------------------------------------------------------
    // WRITE
    // ----------------------------------------------------------------------------------------------------

    public static void Write(object obj, string path)
    {
        var _dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(_dir)) Directory.CreateDirectory(_dir);
        File.WriteAllText(path, Toml.FromModel(BuildTable(obj), _opts));
    }

    private static TomlTable BuildTable(object obj)
    {
        var _type  = obj.GetType();
        var _def   = TryDefault(_type);
        var _table = new TomlTable();

        foreach (var _m in ImpVars(_type))
        {
            var _cur = _m.Get(obj);
            if (_cur is null) continue;

            var _base = _def != null ? _m.Get(_def) : null;
            if (!ValueEquals(_cur, _base))
                _table[_m.Name] = ToToml(_cur, _m.Type);
        }
        return _table;
    }

    // ----------------------------------------------------------------------------------------------------
    // READ
    // ----------------------------------------------------------------------------------------------------

    public static void Read(object obj, string path)
    {
        if (!File.Exists(path)) return;
        ReadFromTable(obj, new Prase.parse_toml(Toml.ToModel(File.ReadAllText(path), null, _opts)));
    }

    internal static void ReadFromTable(object obj, ImpParse parser)
    {
        foreach (var _m in ImpVars(obj.GetType()))
        {
            var _raw = parser.GetRaw(_m.Name);
            if (_raw != null) _m.Set(obj, FromToml(_raw, _m.Type));
        }
    }

    // ----------------------------------------------------------------------------------------------------
    // SERIALIZATION
    // ----------------------------------------------------------------------------------------------------

    private static object ToToml(object val, Type type)
    {
        // I_PropertyType custom hook — checked first so structs can override anything below
        if (val is I_PropertyType _pt && _pt.Savable_ToToml() is { } _custom)
            return _custom;

        // ImpAsset: reference (path string) or inline sub-table depending on serialized flag
        if (val is ImpAsset _asset)
        {
            if (_asset.serialized && _asset.serialPath != null)
                return _asset.serialPath;

            var _sub = BuildTable(_asset);
            _sub["__type"] = _asset.GetType().Name;
            return _sub;
        }

        // Dictionary<TKey,TValue> — string keys → TOML table; other key types → array of {k,v} pairs
        if (val is IDictionary _dict && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var _args    = type.GetGenericArguments();
            var _keyType = _args[0];
            var _valType = _args[1];
            if (_keyType == typeof(string))
            {
                var _tbl = new TomlTable();
                foreach (DictionaryEntry _e in _dict)
                    _tbl[_e.Key.ToString()!] = ToToml(_e.Value!, _valType);
                return _tbl;
            }
            var _arr2 = new TomlArray();
            foreach (DictionaryEntry _e in _dict)
            {
                var _pair = new TomlTable { ["k"] = ToToml(_e.Key, _keyType), ["v"] = ToToml(_e.Value!, _valType) };
                _arr2.Add(_pair);
            }
            return _arr2;
        }

        // List<T> — recurse so nested ImpAssets / structs in lists work too
        if (val is IList _list && type.IsGenericType)
        {
            var _elemType = type.GetGenericArguments()[0];
            var _arr      = new TomlArray();
            foreach (var _e in _list)
                if (_e != null) _arr.Add(ToToml(_e, _elemType));
            return _arr;
        }

        // Struct/object with [ImpVar] members: recurse as sub-table (no __type; static field type is sufficient)
        if (!type.IsPrimitive && type != typeof(string) && !type.IsEnum && ImpVars(type).Any())
            return BuildTable(val);

        // System.Numerics / Color — explicit serialisation so Tomlyn never sees raw structs.
        if (type == typeof(Vector2))    { var v = (Vector2)val;    return new TomlTable { ["x"] = (double)v.X, ["y"] = (double)v.Y }; }
        if (type == typeof(Vector3))    { var v = (Vector3)val;    return new TomlTable { ["x"] = (double)v.X, ["y"] = (double)v.Y, ["z"] = (double)v.Z }; }
        if (type == typeof(Vector4))    { var v = (Vector4)val;    return new TomlTable { ["x"] = (double)v.X, ["y"] = (double)v.Y, ["z"] = (double)v.Z, ["w"] = (double)v.W }; }
        if (type == typeof(Quaternion)) { var q = (Quaternion)val; return new TomlTable { ["x"] = (double)q.X, ["y"] = (double)q.Y, ["z"] = (double)q.Z, ["w"] = (double)q.W }; }
        if (type == typeof(Color))      { var c = (Color)val;      return (long)c.ToArgb(); }

        return val; // primitives, strings, enums — Tomlyn handles natively
    }

    private static object? FromToml(object raw, Type target)
    {
        // I_PropertyType custom hook
        if (typeof(I_PropertyType).IsAssignableFrom(target) && TryDefault(target) is I_PropertyType _pt)
        {
            var _custom = _pt.Savable_FromToml(raw);
            if (_custom != null) return _custom;
        }

        // ImpAsset: string → load from file (reference), TomlTable → deserialize inline
        if (typeof(ImpAsset).IsAssignableFrom(target))
        {
            if (raw is string _path)
            {
                var _asset = (ImpAsset)Activator.CreateInstance(target)!;
                Read(_asset, _path);
                _asset.serialized = true;
                _asset.serialPath = _path;
                return _asset;
            }
            if (raw is TomlTable _t)
            {
                var _concrete = target;
                if (_t.TryGetValue("__type", out var _tn) && _tn is string _name)
                    _concrete = ResolveType(_name, target) ?? target;
                var _asset = (ImpAsset)Activator.CreateInstance(_concrete)!;
                ReadFromTable(_asset, new Prase.parse_toml(_t));
                return _asset;
            }
            return null;
        }

        // Primitive coercion — Tomlyn stores integers as long, floats as double
        if (target == typeof(int)   && raw is long  _li) return (int)_li;
        if (target == typeof(uint)  && raw is long  _lu) return (uint)_lu;
        if (target == typeof(short) && raw is long  _ls) return (short)_ls;
        if (target == typeof(float) && raw is double _d) return (float)_d;

        // Dictionary<TKey,TValue>
        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var _args    = target.GetGenericArguments();
            var _keyType = _args[0];
            var _valType = _args[1];
            var _dict    = (IDictionary)Activator.CreateInstance(target)!;
            if (_keyType == typeof(string) && raw is TomlTable _tbl)
            {
                foreach (var _kvp in _tbl)
                    _dict[_kvp.Key] = FromToml(_kvp.Value, _valType)!;
                return _dict;
            }
            if (raw is TomlArray _dictArr)
            {
                foreach (var _item in _dictArr)
                    if (_item is TomlTable _pair &&
                        _pair.TryGetValue("k", out var _k) && _k != null &&
                        _pair.TryGetValue("v", out var _v) && _v != null)
                    {
                        var _key = FromToml(_k, _keyType);
                        if (_key != null) _dict[_key] = FromToml(_v, _valType)!;
                    }
                return _dict;
            }
            return _dict;
        }

        // List<T> from TomlArray
        if (raw is TomlArray _arr && target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>))
            return ConvertArray(_arr, target);

        // Struct/object with [ImpVar] members: read sub-table recursively
        if (raw is TomlTable _table && ImpVars(target).Any())
        {
            var _obj = Activator.CreateInstance(target)!;
            ReadFromTable(_obj, new Prase.parse_toml(_table));
            return _obj;
        }

        // System.Numerics / Color
        if (raw is TomlTable _nt)
        {
            static float F(TomlTable t, string k) =>
                t.TryGetValue(k, out var v) && v is double d ? (float)d : 0f;

            if (target == typeof(Vector2))    return new Vector2(F(_nt,"x"), F(_nt,"y"));
            if (target == typeof(Vector3))    return new Vector3(F(_nt,"x"), F(_nt,"y"), F(_nt,"z"));
            if (target == typeof(Vector4))    return new Vector4(F(_nt,"x"), F(_nt,"y"), F(_nt,"z"), F(_nt,"w"));
            if (target == typeof(Quaternion)) return new Quaternion(F(_nt,"x"), F(_nt,"y"), F(_nt,"z"), F(_nt,"w"));
        }
        if (target == typeof(Color) && raw is long _argb) return Color.FromArgb((int)_argb);

        return raw;
    }

    private static IList ConvertArray(TomlArray arr, Type listType)
    {
        var _elemType = listType.GetGenericArguments()[0];
        var _list     = (IList)Activator.CreateInstance(listType)!;
        foreach (var _e in arr)
            if (_e != null) _list.Add(FromToml(_e, _elemType));
        return _list;
    }

    // ----------------------------------------------------------------------------------------------------
    // INTERNALS
    // ----------------------------------------------------------------------------------------------------

    private static object? TryDefault(Type type)
    {
        try { return Activator.CreateInstance(type); } catch { return null; }
    }

    private static bool ValueEquals(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Equals(b)) return true;

        // ImpAsset: compare by serialPath for refs, or by type for inline (always write)
        if (a is ImpAsset _aa && b is ImpAsset _ab)
            return _aa.serialized && _ab.serialized && _aa.serialPath == _ab.serialPath;

        // Structural equality for lists/value objects via Tomlyn round-trip
        try
        {
            return Toml.FromModel(new Dictionary<string, object?> { ["v"] = a }, _opts)
                == Toml.FromModel(new Dictionary<string, object?> { ["v"] = b }, _opts);
        }
        catch { return false; }
    }

    private static Type? ResolveType(string name, Type baseType)
    {
        foreach (var _asm in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var _t in _asm.GetTypes())
                if (_t.Name == name && baseType.IsAssignableFrom(_t)) return _t;
        return null;
    }

    private readonly record struct Member(
        string Name, Type Type,
        Func<object, object?> Get, Action<object, object?> Set);

    private static IEnumerable<Member> ImpVars(Type type)
    {
        const BindingFlags _f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var _fi in type.GetFields(_f))
            if (_fi.IsDefined(typeof(ImpVarAttribute), true))
                yield return new Member(_fi.Name, _fi.FieldType, _fi.GetValue, _fi.SetValue);

        foreach (var _pi in type.GetProperties(_f))
            if (_pi.CanRead && _pi.CanWrite && _pi.GetIndexParameters().Length == 0
                && _pi.IsDefined(typeof(ImpVarAttribute), true))
                yield return new Member(_pi.Name, _pi.PropertyType, _pi.GetValue, _pi.SetValue);
    }
}


public class ImpSave_Game : ImpSave
{
    
};

public class ImpSave_Global : ImpSave
{
    
};