using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Classes;
using Tomlyn;
using Tomlyn.Model;

namespace Editor;

// Like a normal ImpConfig, but saved to the PROJECT's Config folder as Editor.toml. Persists
// editor session state between runs: last opened level, viewport camera, layout splits, and
// which tool windows were open. The scalar fields ride the same [ImpVar] serialization the
// engine uses for everything else; only the window list needs a hand-rolled array.
public class EditorConfig : ImpConfig
{
    // last opened level, stored keyword-relative ({game}/...) so it stays machine-portable
    [ImpVar] public string last_level = "";

    // editor viewport free-fly camera (defaults mirror PNL_World's)
    [ImpVar] public Vector3 cam_position = new(8, 6, 8);
    [ImpVar] public float cam_yaw = -2.35f;
    [ImpVar] public float cam_pitch = -0.45f;
    [ImpVar] public float cam_fov = 60f;

    // level-editor right-column layout
    [ImpVar] public float side_width = 340f;
    [ImpVar] public float side_split = 0.45f;

    // tool windows open at save time (besides the always-on level editor), by type name
    public List<string> open_windows = new();

    const string OpenWindowsKey = "open_windows";

    public static string PathFor(string projectDir) =>
        Path.Combine(projectDir, "Config", "Editor.toml");

    public static EditorConfig Load(string path)
    {
        var cfg = new EditorConfig();
        if (!File.Exists(path)) return cfg;
        try
        {
            var table = Toml.ToModel(File.ReadAllText(path));
            ImpToml.ReadParams(cfg, table);
            if (table.TryGetValue(OpenWindowsKey, out object? o) && o is TomlArray arr)
                foreach (object? v in arr)
                    if (v is string s) cfg.open_windows.Add(s);
        }
        catch (Exception ex) { Console.WriteLine($"[EditorConfig] Load failed: {ex.Message}"); }
        return cfg;
    }

    public bool Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var doc = ImpToml.WriteParams(this);   // every [ImpVar] field differing from default
            if (open_windows.Count > 0)
            {
                var arr = new TomlArray();
                foreach (string w in open_windows) arr.Add(w);
                doc[OpenWindowsKey] = arr;
            }

            File.WriteAllText(path, Toml.FromModel(doc));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EditorConfig] Save failed: {ex.Message}");
            return false;
        }
    }
}
