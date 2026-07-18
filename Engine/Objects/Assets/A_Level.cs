using System.Collections;
using System.Numerics;
using System.Reflection;
using ImperiumEngine.Classes;
using ImperiumEngine.Structs;
using Raylib_cs;
using Tomlyn;
using Tomlyn.Model;

namespace ImperiumEngine.Objects.Assets;

// a level is a collection of entities
public class A_Level : A_Entity
{
    public override bool Load(string path)
    {
        base.Load(path);
        components.Clear();

        string toml;
        try { toml = File.ReadAllText(path); }
        catch (Exception ex)
        {
            Console.WriteLine($"[A_Level] Could not read {path}: {ex.Message}");
            return false;
        }

        TomlTable table;
        try { table = Toml.ToModel(toml); }
        catch (Exception ex)
        {
            Console.WriteLine($"[A_Level] TOML parse error in {path}: {ex.Message}");
            return false;
        }

        if (!table.TryGetValue("entity", out object? entitiesObj) ||
            entitiesObj is not TomlTableArray entities)
        {
            Console.WriteLine($"[A_Level] No [[entity]] entries found in {path}");
            return false;
        }

        var asm = Assembly.GetExecutingAssembly();

        foreach (TomlTable entity in entities)
        {
            string typeName = entity.TryGetValue("type", out object? t) ? t!.ToString()! : "";
            if (string.IsNullOrEmpty(typeName)) continue;

            Type? componentType =
                asm.GetType($"ImperiumEngine.Objects._3D.{typeName}") ??
                asm.GetType($"ImperiumEngine.Objects._2D.{typeName}") ??
                asm.GetType($"ImperiumEngine.Objects._1D.{typeName}");

            if (componentType == null || !componentType.IsAssignableTo(typeof(ImpComponent)))
            {
                Console.WriteLine($"[A_Level] Unknown entity type: {typeName}");
                continue;
            }

            var component = (ImpComponent)Activator.CreateInstance(componentType)!;

            // Transform is always a special sub-table, not an [ImpVar]
            if (component is ImpComponent3D c3d &&
                entity.TryGetValue("transform", out object? tr) && tr is TomlTable trt)
                c3d.transform = ParseTransform(trt);

            // Apply all [ImpVar] fields from [entity.params]
            if (entity.TryGetValue("params", out object? p) && p is TomlTable paramsTable)
                ApplyParams(component, paramsTable);

            components.Add(component);
        }

        Console.WriteLine($"[A_Level] Loaded '{Path.GetFileName(path)}' — {components.Count} entities");
        return true;
    }

    // Walks the full type hierarchy and sets every [ImpVar] field found in paramsTable
    static void ApplyParams(ImpComponent target, TomlTable paramsTable)
    {
        var type = target.GetType();
        while (type != null && type != typeof(object))
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!field.IsDefined(typeof(ImpVarAttribute), false)) continue;
                if (!paramsTable.TryGetValue(field.Name, out object? raw)) continue;

                var value = ConvertTomlValue(raw, field.FieldType);
                if (value != null) field.SetValue(target, value);
            }
            type = type.BaseType;
        }
    }

    static object? ConvertTomlValue(object? raw, Type target)
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
        return null;
    }

    // ------------------------------------------------------------------
    // TOML → engine type converters (also used by sub-systems that parse
    // non-param tables like [entity.transform])
    // ------------------------------------------------------------------

    internal static TTransform3D ParseTransform(TomlTable t)
    {
        var result = new TTransform3D();
        if (t.TryGetValue("position", out object? p)) result.Position = ToVec3(p);
        if (t.TryGetValue("rotation", out object? r)) result.Rotation = ToVec3(r);
        if (t.TryGetValue("scale",    out object? s)) result.Scale    = ToVec3(s, Vector3.One);
        return result;
    }

    // Tomlyn's TomlArray only implements IList<object?>, never non-generic IList,
    // so element access must go through IEnumerable.
    static float[]? ToFloats(object? obj)
    {
        if (obj is string || obj is not IEnumerable e) return null;
        return e.Cast<object?>().Select(Convert.ToSingle).ToArray();
    }

    internal static Vector3 ToVec3(object? obj, Vector3 fallback = default)
    {
        if (ToFloats(obj) is { Length: >= 3 } f)
            return new Vector3(f[0], f[1], f[2]);
        return fallback;
    }

    internal static Vector2 ToVec2(object? obj, Vector2 fallback = default)
    {
        if (ToFloats(obj) is { Length: >= 2 } f)
            return new Vector2(f[0], f[1]);
        return fallback;
    }

    // [r, g, b] or [r, g, b, a] floats in 0–1 range → Raylib Color (0–255)
    internal static Color ToColor(object? obj, Color fallback = default)
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
