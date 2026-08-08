using System.Reflection;
using ImperiumEngine.Classes;
using Tomlyn;
using Tomlyn.Model;

namespace ImperiumEngine.Objects.Assets;

// a level is a collection of entities
public class A_Level : A_Entity
{
    [ImpVar] public A_GameMode game_mode;

    protected override string Editor_GetExtension() => ".ImpLvl";

    public override bool File_Load(string path)
    {
        base.File_Load(path);

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

        if (!LoadTable(table))
        {
            Console.WriteLine($"[A_Level] No [[entity]] entries found in {path}");
            return false;
        }

        Console.WriteLine($"[A_Level] Loaded '{Path.GetFileName(path)}' — {components.Count} entities");
        return true;
    }

    // Reads the level's own params + all entities from an already-parsed TOML document. Shared by
    // File_Load (from disk) and Clone (from an in-memory snapshot). Returns false if the document
    // has no [[entity]] array.
    bool LoadTable(TomlTable table)
    {
        components.Clear();

        // the level's own [ImpVar] fields (game_mode, ...) live in a top-level [params] table
        if (table.TryGetValue("params", out object? levelParamsObj) && levelParamsObj is TomlTable levelParams)
            ImpToml.ReadParams(this, levelParams);

        if (!table.TryGetValue("entity", out object? entitiesObj) ||
            entitiesObj is not TomlTableArray entities)
            return false;

        // each [[entity]] is a full tree (type + params + nested children) — see WriteComponentNode
        foreach (TomlTable entity in entities)
        {
            var component = ReadComponentNode(entity);
            if (component != null)
                components.Add(component);
        }
        return true;
    }

    // A deep copy of the level, produced by round-tripping through the same TOML model used to
    // save/load. Used by Play-In-Editor so the running instance is fully isolated from the edited
    // level (edits during play are discarded on stop).
    public A_Level Clone()
    {
        var clone = new A_Level { file_link = file_link };
        clone.LoadTable(BuildDoc());
        return clone;
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

    // Builds the level's TOML document ([params] + [[entity]] array). Shared by Save (to disk) and
    // Clone (in-memory deep copy).
    public TomlTable BuildDoc()
    {
        var doc = new TomlTable();

        // the level's own [ImpVar] fields (game_mode, ...), omitting anything left at default.
        // Written before [[entity]] so it reads as a top-level [params] table, not the last entity's.
        var levelParams = new TomlTable();
        ImpToml.WriteParams(this, new A_Level(), levelParams);
        if (levelParams.Count > 0) doc["params"] = levelParams;

        var entities = new TomlTableArray();

        // root components only — each node recursively embeds its Children hierarchy
        foreach (var component in components)
            entities.Add(WriteComponentNode(component));

        doc["entity"] = entities;
        return doc;
    }

    public bool Save(string path)
    {
        try
        {
            File.WriteAllText(path, Toml.FromModel(BuildDoc()));
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
