using System.Collections;
using System.Numerics;
using System.Reflection;
using ImperiumEngine.Interfaces;
using Raylib_cs;
using Tomlyn.Model;

namespace ImperiumEngine.Classes;

// Shared TOML (de)serialization for [ImpVar] fields, including nested ImpAsset slots.
//
// An [ImpVar] field of an ImpAsset-derived type is a Godot-style resource slot and is
// written one of two ways:
//   * Reference — the asset was loaded from disk (its file_link is set). Stored as a
//     string path in keyword form, e.g. "{game}/Movement/Walk.impasset".
//   * Instance  — an embedded asset with no backing file (file_link empty). Stored as an
//     inline sub-table carrying "_type" plus the asset's own params, so its data lives
//     directly in the parent's file. Instances may nest to any depth.
// A missing value means null (empty slot).
public static class ImpToml
{
    public const string TypeKey = "_type";

    public static bool IsAssetType(Type t) => typeof(ImpAsset).IsAssignableFrom(t);

    // ------------------------------------------------------------------
    // object <-> params table
    // ------------------------------------------------------------------

    // Builds a params table of every [ImpVar] field that differs from a fresh default.
    public static TomlTable WriteParams(object target)
    {
        var table = new TomlTable();
        WriteParams(target, TryCreateDefault(target.GetType()), table);
        return table;
    }

    // Writes into an existing table, omitting fields equal to the supplied reference's
    // value (a fresh default instance). Pass reference = null to write every set field.
    public static void WriteParams(object target, object? reference, TomlTable table)
    {
        for (var type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!field.IsDefined(typeof(ImpVarAttribute), false)) continue;

            var value = field.GetValue(target);
            var toml = ToTomlValue(value, field.FieldType);
            if (toml == null) continue;   // unsupported / non-serializable type

            var def = reference != null ? field.GetValue(reference) : null;
            if (AreEqual(value, def, field.FieldType)) continue;   // unchanged from default

            table[field.Name] = toml;
        }
    }

    // Applies every [ImpVar] field found in the params table.
    public static void ReadParams(object target, TomlTable table)
    {
        for (var type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!field.IsDefined(typeof(ImpVarAttribute), false)) continue;
            if (!table.TryGetValue(field.Name, out object? raw)) continue;

            var value = ConvertTomlValue(raw, field.FieldType);
            // Asset slots may legitimately resolve to null (empty / broken ref); still assign
            // so the field reflects the stored intent. Other types keep their default on failure.
            if (value != null || IsAssetType(field.FieldType))
                field.SetValue(target, value);
        }
    }

    // ------------------------------------------------------------------
    // single value <-> Tomlyn model
    // ------------------------------------------------------------------

    // Engine type → Tomlyn model value. Returns null for anything ConvertTomlValue can't
    // read back, so those fields are simply not written.
    public static object? ToTomlValue(object? value, Type type)
    {
        if (value == null) return null;
        if (type == typeof(float))   return (double)(float)value;
        if (type == typeof(double))  return (double)value;
        if (type == typeof(int))     return (long)(int)value;
        if (type == typeof(long))    return (long)value;
        if (type == typeof(bool))    return (bool)value;
        if (type == typeof(string))  return (string)value;
        if (type == typeof(Vector3)) return Vec3Array((Vector3)value);
        if (type == typeof(Vector2)) return Vec2Array((Vector2)value);
        if (type == typeof(Color))   return ColorArray((Color)value);
        if (type.IsEnum)             return value.ToString();
        if (IsAssetType(type))       return AssetToToml((ImpAsset)value);

        // collections — an array of serialized elements, or a keyed table for dictionaries.
        if (IsList(type, out var elemType)) return ListToToml((IEnumerable)value, elemType);
        if (IsDict(type, out var keyType, out var valType)) return DictToToml((IDictionary)value, keyType, valType);

        // structs / classes: custom logic via I_Serialize, else cascade their [ImpVar] fields
        // into a sub-table. Either way an empty result is dropped (nothing worth writing).
        if (value is I_Serialize ser)
        {
            var custom = new TomlTable();
            ser.File_WriteTo(custom);
            return custom.Count > 0 ? custom : null;
        }
        if (IsComposite(type))
        {
            var table = WriteParams(value);   // recursive, with fresh-default omission
            return table.Count > 0 ? table : null;
        }
        return null;
    }

    public static object? ConvertTomlValue(object? raw, Type target)
    {
        if (raw == null) return null;
        if (target == typeof(float))   return Convert.ToSingle(raw);
        if (target == typeof(double))  return Convert.ToDouble(raw);
        if (target == typeof(int))     return Convert.ToInt32(raw);
        if (target == typeof(long))    return Convert.ToInt64(raw);
        if (target == typeof(bool))    return (bool)raw;
        if (target == typeof(string))  return raw.ToString()!;
        if (target == typeof(Vector3)) return ToVec3(raw);
        if (target == typeof(Vector2)) return ToVec2(raw);
        if (target == typeof(Color))   return ToColor(raw);
        if (target.IsEnum)             return Enum.Parse(target, raw.ToString()!);
        if (IsAssetType(target))       return TomlToAsset(raw, target);

        // collections (mirror ToTomlValue above)
        if (IsList(target, out var elemType)) return TomlToList(raw, target, elemType);
        if (IsDict(target, out var keyType, out var valType)) return TomlToDict(raw, target, keyType, valType);

        // structs / classes read from their sub-table (mirrors ToTomlValue above)
        if (raw is TomlTable table && IsComposite(target))
        {
            object? obj = Activator.CreateInstance(target);   // runs the parameterless ctor (struct defaults apply)
            if (obj == null) return null;
            if (obj is I_Serialize ser) { ser.File_ReadFrom(table); return ser; }  // boxed struct mutated in place
            ReadParams(obj, table);
            return obj;
        }
        return null;
    }

    // A struct or class we can round-trip as a nested table: not a primitive/enum/string, and
    // constructible (value types always are, even without an explicit parameterless ctor).
    static bool IsComposite(Type t) =>
        t != typeof(string) && !t.IsPrimitive && !t.IsEnum &&
        (t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null);

    // ------------------------------------------------------------------
    // collections — List<T> ↔ TomlArray, Dictionary<K,V> ↔ keyed TomlTable
    // ------------------------------------------------------------------

    public static bool IsList(Type t, out Type elem)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        { elem = t.GetGenericArguments()[0]; return true; }
        elem = typeof(object);
        return false;
    }

    public static bool IsDict(Type t, out Type key, out Type val)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        { var a = t.GetGenericArguments(); key = a[0]; val = a[1]; return true; }
        key = val = typeof(object);
        return false;
    }

    // Table-shaped elements (structs, assets, nested collections) round-trip as sub-tables, so
    // an "empty" element still needs a placeholder table to preserve list length / dict entries.
    // Scalar-shaped elements (primitives, enums, strings, vectors, colors) never need this.
    static bool IsTableShaped(Type t) =>
        !(t.IsPrimitive || t.IsEnum || t == typeof(string) ||
          t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Color));

    static TomlArray? ListToToml(IEnumerable list, Type elemType)
    {
        var arr = new TomlArray();
        foreach (var item in list)
        {
            var toml = ToTomlValue(item, elemType);
            if (toml == null && IsTableShaped(elemType)) toml = new TomlTable();  // empty element placeholder
            if (toml != null) arr.Add(toml);
        }
        return arr.Count > 0 ? arr : null;   // omit empty lists entirely
    }

    static object? TomlToList(object raw, Type listType, Type elemType)
    {
        // Tomlyn parses inline arrays as TomlArray but [[table]] arrays as TomlTableArray; accept
        // any enumerable so both an array of primitives and an array of tables read back.
        if (raw is string || raw is not IEnumerable items) return null;
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            var val = ConvertTomlValue(item, elemType);
            list.Add(val ?? (elemType.IsValueType ? Activator.CreateInstance(elemType) : null));
        }
        return list;
    }

    static TomlTable? DictToToml(IDictionary dict, Type keyType, Type valType)
    {
        var table = new TomlTable();
        foreach (DictionaryEntry e in dict)
        {
            var toml = ToTomlValue(e.Value, valType);
            if (toml == null && IsTableShaped(valType)) toml = new TomlTable();
            if (toml != null) table[KeyToString(e.Key)] = toml;
        }
        return table.Count > 0 ? table : null;
    }

    static object? TomlToDict(object raw, Type dictType, Type keyType, Type valType)
    {
        if (raw is not TomlTable table) return null;
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;
        foreach (var kv in table)
        {
            var key = StringToKey(kv.Key, keyType);
            if (key == null) continue;
            var val = ConvertTomlValue(kv.Value, valType);
            dict[key] = val ?? (valType.IsValueType ? Activator.CreateInstance(valType) : null);
        }
        return dict;
    }

    // Dictionary keys become TOML table keys, which must be strings. String / enum / integral /
    // Guid keys round-trip; anything else falls back to ToString (may not read back cleanly).
    static string KeyToString(object key) => key.ToString() ?? "";

    static object? StringToKey(string s, Type keyType)
    {
        if (keyType == typeof(string)) return s;
        if (keyType.IsEnum)            return Enum.TryParse(keyType, s, out var e) ? e : null;
        if (keyType == typeof(int))    return int.TryParse(s, out var i) ? i : null;
        if (keyType == typeof(long))   return long.TryParse(s, out var l) ? l : null;
        if (keyType == typeof(Guid))   return Guid.TryParse(s, out var g) ? g : null;
        return null;
    }

    // ------------------------------------------------------------------
    // asset slots
    // ------------------------------------------------------------------

    // reference → path string; instance → { _type, ...params } inline table
    static object AssetToToml(ImpAsset asset)
    {
        if (!string.IsNullOrEmpty(asset.file_link))
            return ImpAsset.ToKeywordPath(asset.file_link);

        var table = new TomlTable { [TypeKey] = asset.GetType().Name };
        WriteParams(asset, TryCreateDefault(asset.GetType()), table);
        return table;
    }

    static ImpAsset? TomlToAsset(object raw, Type declaredType)
    {
        if (raw is string path)                     // reference — load from disk
            return ImpAsset.LoadFile(ImpAsset.ResolvePath(path), declaredType);

        if (raw is TomlTable table)                 // embedded instance
        {
            var type = ResolveAssetType(table, declaredType);
            if (type == null) return null;
            var asset = (ImpAsset)Activator.CreateInstance(type)!;
            ReadParams(asset, table);               // file_link stays "" → marks it an instance
            return asset;
        }
        return null;
    }

    // Resolves the concrete asset type from a table's "_type" tag, falling back to the
    // declared slot type when it is itself concrete.
    public static Type? ResolveAssetType(TomlTable table, Type declared)
    {
        if (table.TryGetValue(TypeKey, out object? t) && t is string name && FindAssetType(name) is { } found)
            return found;
        return declared != typeof(ImpAsset) && !declared.IsAbstract ? declared : null;
    }

    public static Type? FindAssetType(string name)
    {
        foreach (var t in typeof(ImpAsset).Assembly.GetTypes())
            if (t.Name == name && IsAssetType(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                return t;
        return null;
    }

    // Concrete, default-constructible asset types assignable to baseType (for a "New" menu).
    public static IEnumerable<Type> ConcreteAssetTypes(Type baseType)
    {
        foreach (var t in typeof(ImpAsset).Assembly.GetTypes())
            if (baseType.IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                yield return t;
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    static object? TryCreateDefault(Type t)
    {
        try { return Activator.CreateInstance(t); }
        catch { return null; }
    }

    // Any assigned asset (ref or instance) is considered changed vs. the null default,
    // so a populated slot always serializes.
    static bool AreEqual(object? a, object? b, Type type)
    {
        if (IsAssetType(type)) return a == null && b == null;
        return Equals(a, b);
    }

    public static TomlArray Vec3Array(Vector3 v)
    {
        var a = new TomlArray();
        a.Add((double)v.X); a.Add((double)v.Y); a.Add((double)v.Z);
        return a;
    }

    public static TomlArray Vec2Array(Vector2 v)
    {
        var a = new TomlArray();
        a.Add((double)v.X); a.Add((double)v.Y);
        return a;
    }

    // Raylib Color (0–255) → [r, g, b] (+ a when not fully opaque) floats in 0–1.
    public static TomlArray ColorArray(Color c)
    {
        var a = new TomlArray();
        a.Add(c.R / 255.0); a.Add(c.G / 255.0); a.Add(c.B / 255.0);
        if (c.A != 255) a.Add(c.A / 255.0);
        return a;
    }

    // Tomlyn's TomlArray only implements IList<object?>, never non-generic IList, so element
    // access must go through IEnumerable.
    static float[]? ToFloats(object? obj)
    {
        if (obj is string || obj is not IEnumerable e) return null;
        return e.Cast<object?>().Select(Convert.ToSingle).ToArray();
    }

    public static Vector3 ToVec3(object? obj, Vector3 fallback = default)
    {
        if (ToFloats(obj) is { Length: >= 3 } f) return new Vector3(f[0], f[1], f[2]);
        return fallback;
    }

    public static Vector2 ToVec2(object? obj, Vector2 fallback = default)
    {
        if (ToFloats(obj) is { Length: >= 2 } f) return new Vector2(f[0], f[1]);
        return fallback;
    }

    // [r, g, b] or [r, g, b, a] floats in 0–1 range → Raylib Color (0–255)
    public static Color ToColor(object? obj, Color fallback = default)
    {
        if (ToFloats(obj) is { Length: >= 3 } f)
            return new Color(
                (byte)(f[0] * 255f),
                (byte)(f[1] * 255f),
                (byte)(f[2] * 255f),
                f.Length >= 4 ? (byte)(f[3] * 255f) : (byte)255);
        return fallback;
    }
}
