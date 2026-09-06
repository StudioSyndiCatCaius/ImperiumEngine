using System.Reflection;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Globals;

public static class GConfig
{
    static List<Type>? _types;

    public static string GamePath()
    {
        string root = GFile.GetDir_Root(EContentDir.Game);
        if (string.IsNullOrEmpty(root)) return "";
        return Path.Combine(root, "Config", "Game.toml");
    }

    public static List<Type> Gather()
    {
        if (_types != null) return _types;
        List<Type> list = new();
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            foreach (Type t in types)
            {
                if (t == null || !t.IsClass) continue;
                bool any = false;
                foreach (MemberInfo _ in TTable.Config_Members(t, true))
                {
                    any = true;
                    break;
                }
                if (any) list.Add(t);
            }
        }
        list.Sort((a, b) => string.Compare(Title(a), Title(b), StringComparison.OrdinalIgnoreCase));
        _types = list;
        return list;
    }

    public static string Title(Type t) =>
        t.GetCustomAttribute<TitleAttribute>()?.Name ?? t.Name;

    public static void LoadGame()
    {
        string path = GamePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        TTable tbl;
        try { tbl = TTable.FromTOML(File.ReadAllText(path)); }
        catch (Exception e)
        {
            GLog.Warning("Game.toml load failed: " + e.Message);
            return;
        }
        foreach (Type t in Gather())
        {
            TTable vars = tbl.get_Table(t.Name);
            if (vars == null) continue;
            TTable.PopulateConfig(vars, t, null, true);
        }
    }

    public static void SaveGame()
    {
        string path = GamePath();
        if (string.IsNullOrEmpty(path)) return;
        TTable tbl = new();
        foreach (Type t in Gather())
        {
            TTable vars = TTable.FromConfig(t, null, true);
            if (vars.data.Count == 0) continue;
            tbl.Set(t.Name, vars);
        }
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, TTable.ToTOML(tbl));
    }
}
