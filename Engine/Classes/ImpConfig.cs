using Tomlyn;

namespace ImperiumEngine.Classes;

// Baseclass for a config file. Like UE's DevSettings. Each config persists its [ImpVar] fields
// to its own TOML file in the project's Config folder, named after the class minus its "CFG_"
// prefix (CFG_Graphics -> Config/Graphics.toml). Uses the same [ImpVar] serialization the engine
// applies to levels and save files, so scalar / enum / vector / struct fields round-trip for free.
public abstract class ImpConfig
{
    // File-name stem for this config: the class name without its "CFG_" prefix.
    public virtual string ConfigName =>
        GetType().Name.StartsWith("CFG_") ? GetType().Name["CFG_".Length..] : GetType().Name;

    // Absolute path to this config's file inside the given project directory.
    public string FilePath(string projectDir) => PathFor(projectDir, ConfigName);

    public static string PathFor(string projectDir, string name) =>
        Path.Combine(projectDir, "Config", name + ".toml");

    // Reads every [ImpVar] field present in the file; missing files / fields keep their defaults.
    public virtual void Load(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var table = Toml.ToModel(File.ReadAllText(path));
            ImpToml.ReadParams(this, table);
        }
        catch (Exception ex) { Console.WriteLine($"[{GetType().Name}] Load failed: {ex.Message}"); }
    }

    // Writes every [ImpVar] field that differs from a fresh default back to the file.
    public virtual bool Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Toml.FromModel(ImpToml.WriteParams(this)));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{GetType().Name}] Save failed: {ex.Message}");
            return false;
        }
    }
}
