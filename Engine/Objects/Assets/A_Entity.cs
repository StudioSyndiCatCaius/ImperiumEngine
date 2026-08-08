using System.Reflection;
using ImperiumEngine.Classes;
using Tomlyn;
using Tomlyn.Model;

namespace ImperiumEngine.Objects.Assets;

// a preset of components in a heriarchy. entities themselves then are treated like a new component (a child of whatever their given parent type is)
public class A_Entity : ImpAsset
{
    public string parent_type = "";
    public List<ImpComponent> components = new();

    protected override string Editor_GetExtension() => ".ImpEnt";

    // Resolves a component's simple type name the same way A_Level does. Looks in the 3D/2D/1D
    // object namespaces first, then ImperiumEngine.Classes (ImpComponent / ImpComponent2D / 3D /
    // ImpPhysic3D — usable as empty "null" bases in the hierarchy). Shared with the editor.
    public static Type? ResolveComponentType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        var asm = Assembly.GetExecutingAssembly();
        return asm.GetType($"ImperiumEngine.Objects._3D.{typeName}") ??
               asm.GetType($"ImperiumEngine.Objects._2D.{typeName}") ??
               asm.GetType($"ImperiumEngine.Objects._1D.{typeName}") ??
               asm.GetType($"ImperiumEngine.Classes.{typeName}");
    }

    // ------------------------------------------------------------------
    // Component tree serdes — shared by A_Entity presets and A_Level.
    // Each node: { type, params?, children? } where children is a nested table array of the
    // same shape. Older files without `children` still load as flat roots.
    // ------------------------------------------------------------------

    // Writes one component and its full child hierarchy into a TOML table.
    public static TomlTable WriteComponentNode(ImpComponent component)
    {
        var type = component.GetType();
        var table = new TomlTable { ["type"] = type.Name };

        // fresh defaults so unchanged [ImpVar]s are omitted
        var reference = (ImpComponent)Activator.CreateInstance(type)!;
        var paramsTable = new TomlTable();
        ImpToml.WriteParams(component, reference, paramsTable);
        if (paramsTable.Count > 0) table["params"] = paramsTable;

        if (component.Children.Count > 0)
        {
            var kids = new TomlTableArray();
            foreach (var child in component.Children)
                kids.Add(WriteComponentNode(child));
            table["children"] = kids;
        }

        return table;
    }

    // Reconstructs a component (and nested children) from WriteComponentNode output.
    // Returns null if the type is unknown / not a component.
    public static ImpComponent? ReadComponentNode(TomlTable table)
    {
        string typeName = table.TryGetValue("type", out object? t) ? t?.ToString() ?? "" : "";
        if (string.IsNullOrEmpty(typeName)) return null;

        Type? componentType = ResolveComponentType(typeName);
        if (componentType == null || !componentType.IsAssignableTo(typeof(ImpComponent)))
        {
            Console.WriteLine($"[A_Entity] Unknown entity type: {typeName}");
            return null;
        }

        var component = (ImpComponent)Activator.CreateInstance(componentType)!;
        if (table.TryGetValue("params", out object? p) && p is TomlTable paramsTable)
            ImpToml.ReadParams(component, paramsTable);

        if (table.TryGetValue("children", out object? kidsObj) && kidsObj is TomlTableArray kids)
        {
            foreach (TomlTable kid in kids)
            {
                var child = ReadComponentNode(kid);
                if (child != null)
                    child.Parent_Set(component); // appends, preserves file order
            }
        }

        return component;
    }

    // Serializes to the same [[entity]] layout A_Level uses, plus a top-level parent_type — the
    // component type the entity attaches under when instanced. See A_Level for the shared format.
    public override bool File_Save(string path)
    {
        try { File.WriteAllText(path, Toml.FromModel(BuildDoc())); }
        catch (Exception ex)
        {
            Console.WriteLine($"[A_Entity] Could not write {path}: {ex.Message}");
            return false;
        }
        file_link = path;
        is_dirty = false;
        return true;
    }

    public override bool File_Load(string path)
    {
        file_link = path;

        string toml;
        try { toml = File.ReadAllText(path); }
        catch (Exception ex)
        {
            Console.WriteLine($"[A_Entity] Could not read {path}: {ex.Message}");
            return false;
        }

        TomlTable table;
        try { table = Toml.ToModel(toml); }
        catch (Exception ex)
        {
            Console.WriteLine($"[A_Entity] TOML parse error in {path}: {ex.Message}");
            return false;
        }

        LoadTable(table);
        return true;
    }

    TomlTable BuildDoc()
    {
        var doc = new TomlTable { [ImpToml.TypeKey] = GetType().Name };
        if (!string.IsNullOrEmpty(parent_type)) doc["parent_type"] = parent_type;

        var entities = new TomlTableArray();
        foreach (var component in components)
            entities.Add(WriteComponentNode(component));
        doc["entity"] = entities;
        return doc;
    }

    void LoadTable(TomlTable table)
    {
        components.Clear();

        if (table.TryGetValue("parent_type", out object? pt)) parent_type = pt?.ToString() ?? "";

        if (!table.TryGetValue("entity", out object? entitiesObj) || entitiesObj is not TomlTableArray entities)
            return;

        foreach (TomlTable entity in entities)
        {
            var component = ReadComponentNode(entity);
            if (component != null)
                components.Add(component);
        }
    }
}
