using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;
using Tomlyn;
using Tomlyn.Model;

namespace Editor;

// Discovers user-created entity "classes" (.ImpEnt files) for the TRef<T> dropdown. Each entity
// behaves like a subclass of its root component type (its parent_type), so it appears alongside
// built-in C# classes in a TRef<T> picker whenever that root type is assignable to T.
//
// The project's Content tree is scanned lazily and cached (throttled), since a TRef dropdown may
// be redrawn every frame while open.
static class EntityClasses
{
    // one discovered entity class: its keyword path (as stored in TRef), display name, and the
    // resolved root component type used to decide which TRef<T> dropdowns it belongs in.
    public readonly record struct Entry(string Path, string Name, Type RootType);

    static List<Entry> s_cache = new();
    static long s_next_scan;
    static string s_scanned_dir = "";

    // Entity classes whose root type is assignable to `baseType` (T of a TRef<T>).
    public static List<Entry> ForBase(Type baseType)
    {
        Refresh();
        return s_cache.Where(e => baseType.IsAssignableFrom(e.RootType)).ToList();
    }

    static void Refresh()
    {
        long now = Environment.TickCount64;
        string dir = System.IO.Path.Combine(ImpFile.s_projectDir, "Content");
        if (now < s_next_scan && dir == s_scanned_dir) return;
        s_next_scan = now + 1000;   // re-scan at most once per second
        s_scanned_dir = dir;

        var list = new List<Entry>();
        if (Directory.Exists(dir))
            foreach (var file in Directory.EnumerateFiles(dir, "*.ImpEnt", SearchOption.AllDirectories))
            {
                var root = ReadRootType(file);
                if (root == null) continue;
                list.Add(new Entry(
                    ImpFile.Path_ToRelative(file),
                    System.IO.Path.GetFileNameWithoutExtension(file),
                    root));
            }
        s_cache = list;
    }

    // Reads only the entity's parent_type (its root class) — cheaper than a full A_Entity load.
    static Type? ReadRootType(string file)
    {
        try
        {
            TomlTable table = Toml.ToModel(File.ReadAllText(file));
            if (table.TryGetValue("parent_type", out var pt) && pt is string name)
                return A_Entity.ResolveComponentType(name);
        }
        catch { /* unreadable / malformed entity — skip it */ }
        return null;
    }
}
