using System.Numerics;
using System.Reflection;
using ImperiumEngine.Classes;
using ImperiumEngine.Structs;
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
            string type = entity.TryGetValue("type", out object? t) ? t!.ToString()! : "";
            if (string.IsNullOrEmpty(type)) continue;

            // Find the type by name across object namespaces — no hardcoded type list
            Type? componentType =
                asm.GetType($"ImperiumEngine.Objects._3D.{type}") ??
                asm.GetType($"ImperiumEngine.Objects._2D.{type}") ??
                asm.GetType($"ImperiumEngine.Objects._1D.{type}");

            var fromToml = componentType?.GetMethod("FromToml",
                BindingFlags.Public | BindingFlags.Static);

            if (fromToml?.Invoke(null, [entity]) is ImpComponent component)
                components.Add(component);
            else
                Console.WriteLine($"[A_Level] Unknown or unloadable entity type: {type}");
        }

        Console.WriteLine($"[A_Level] Loaded '{Path.GetFileName(path)}' — {components.Count} entities");
        return true;
    }

    // TOML-to-engine type helpers, used by FromToml() methods on each component type
    internal static TTransform3D ParseTransform(TomlTable t)
    {
        var result = new TTransform3D();
        if (t.TryGetValue("position", out object? p)) result.Position = ToVec3(p);
        if (t.TryGetValue("rotation", out object? r)) result.Rotation = ToVec3(r);
        if (t.TryGetValue("scale",    out object? s)) result.Scale    = ToVec3(s, Vector3.One);
        return result;
    }

    internal static Vector3 ToVec3(object? obj, Vector3 fallback = default)
    {
        if (obj is System.Collections.IList list && list.Count >= 3)
            return new Vector3(
                Convert.ToSingle(list[0]),
                Convert.ToSingle(list[1]),
                Convert.ToSingle(list[2]));
        return fallback;
    }
}
