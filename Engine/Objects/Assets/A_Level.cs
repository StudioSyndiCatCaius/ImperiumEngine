using System.Reflection;
using ImperiumEngine.Classes;
using Tomlyn;
using Tomlyn.Model;

namespace ImperiumEngine.Objects.Assets;

// a level is a collection of entities
public class A_Level : A_Entity
{
    [ImpVar] public A_GameMode game_mode;  
    
    public override bool File_Load(string path)
    {
        base.File_Load(path);
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

            // Apply all [ImpVar] fields from [entity.params] — including `transform`, which
            // serializes itself via I_Serialize (see TTransform3D/TTransform2D).
            if (entity.TryGetValue("params", out object? p) && p is TomlTable paramsTable)
                ImpToml.ReadParams(component, paramsTable);

            components.Add(component);
        }

        Console.WriteLine($"[A_Level] Loaded '{Path.GetFileName(path)}' — {components.Count} entities");
        return true;
    }

    // The level owns a bespoke [[entity]] TOML layout, so it routes File_Save (used by the
    // editor's save flow) through its own Save() rather than the base [ImpVar] writer.
    public override bool File_Save(string path)
    {
        if (!Save(path)) return false;
        file_link = path;
        is_dirty = false;
        return true;
    }

    public bool Save(string path)
    {
        var entities = new TomlTableArray();

        foreach (var component in components)
        {
            var type = component.GetType();
            var entity = new TomlTable { ["type"] = type.Name };

            // A fresh instance gives us every field's default, so we can omit
            // anything the user never changed (matching File_Load, which leaves
            // absent fields at their default).
            var reference = (ImpComponent)Activator.CreateInstance(type)!;

            var paramsTable = new TomlTable();
            ImpToml.WriteParams(component, reference, paramsTable);
            if (paramsTable.Count > 0) entity["params"] = paramsTable;

            entities.Add(entity);
        }

        var doc = new TomlTable { ["entity"] = entities };

        try
        {
            File.WriteAllText(path, Toml.FromModel(doc));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[A_Level] Could not write {path}: {ex.Message}");
            return false;
        }

        Console.WriteLine($"[A_Level] Saved '{Path.GetFileName(path)}' — {components.Count} entities");
        return true;
    }
}
