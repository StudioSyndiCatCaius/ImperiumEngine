using Editor.Panel;
using Raylib_cs;

namespace Editor;

/// <summary>
/// Per-folder tint for the content browser, shared by every open browser so the scene tab and the
/// asset tab cannot disagree. Only folders that were explicitly coloured are held here - a folder
/// with no entry inherits from its nearest coloured ancestor, which the tree resolves as it builds.
/// Saved with the rest of the editor session by <see cref="EdState"/>.
/// </summary>
public static class EdFolderColors
{
    /// <summary>Bumped on every change. Trees compare it to know a rebuild is owed.</summary>
    public static int version;

    static readonly Dictionary<string, Color> _colors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Godot set, so a folder colour means the same thing here as it does there.</summary>
    public static readonly (string name, Color color)[] PALETTE =
    {
        ("Red", new Color(255, 118, 118, 255)),
        ("Orange", new Color(255, 168, 96, 255)),
        ("Yellow", new Color(255, 214, 96, 255)),
        ("Green", new Color(132, 219, 132, 255)),
        ("Teal", new Color(96, 214, 204, 255)),
        ("Blue", new Color(120, 170, 255, 255)),
        ("Purple", new Color(178, 140, 255, 255)),
        ("Pink", new Color(255, 130, 196, 255)),
    };

    /// <summary>This folder's own colour, ignoring anything it would inherit. Alpha 0 when unset.</summary>
    public static Color Get(string path)
    {
        string key = Key(path);
        if (key.Length == 0) return default;
        return _colors.TryGetValue(key, out Color c) ? c : default;
    }

    /// <summary>Colours a folder, or clears it with null so it falls back to its ancestor again.</summary>
    public static void Set(string path, Color? color)
    {
        string key = Key(path);
        if (key.Length == 0) return;

        if (color is { A: > 0 } c)
        {
            if (_colors.TryGetValue(key, out Color had) && had.Equals(c)) return;
            _colors[key] = c;
        }
        else if (!_colors.Remove(key)) return;

        version++;
    }

    /// <summary>Follows a rename or move, carrying every coloured folder underneath it along.</summary>
    public static void Path_Moved(string from, string to)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;

        List<KeyValuePair<string, Color>> moved = new();
        foreach (KeyValuePair<string, Color> kv in _colors)
        {
            if (!PNL_FileBrowser.IsUnder(kv.Key, from)) continue;
            moved.Add(kv);
        }
        if (moved.Count == 0) return;

        string root = from.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < moved.Count; i++)
        {
            _colors.Remove(moved[i].Key);
            string dest = PNL_FileBrowser.PathsEqual(moved[i].Key, from)
                ? to
                : to + moved[i].Key[root.Length..];
            _colors[Key(dest)] = moved[i].Value;
        }
        version++;
    }

    /// <summary>Forgets a deleted folder and everything that lived under it.</summary>
    public static void Path_Dropped(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        List<string> gone = new();
        foreach (string key in _colors.Keys)
        {
            if (PNL_FileBrowser.IsUnder(key, path)) gone.Add(key);
        }
        if (gone.Count == 0) return;

        for (int i = 0; i < gone.Count; i++) _colors.Remove(gone[i]);
        version++;
    }

    public static List<(string path, Color color)> Capture()
    {
        List<(string, Color)> list = new();
        foreach (KeyValuePair<string, Color> kv in _colors) list.Add((kv.Key, kv.Value));
        list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static void Apply(IEnumerable<(string path, Color color)> entries)
    {
        _colors.Clear();
        if (entries != null)
        {
            foreach ((string path, Color color) in entries)
            {
                string key = Key(path);
                if (key.Length > 0 && color.A > 0) _colors[key] = color;
            }
        }
        version++;
    }

    /// <summary>"r,g,b,a" - what goes in the TOML. Empty for a colour that is not set.</summary>
    public static string Color_Store(Color c)
    {
        return c.A == 0 ? "" : c.R + "," + c.G + "," + c.B + "," + c.A;
    }

    public static Color Color_Load(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return default;
        string[] parts = text.Split(',');
        if (parts.Length < 3) return default;

        byte Part(int i, byte fallback)
        {
            return i < parts.Length && byte.TryParse(parts[i].Trim(), out byte v) ? v : fallback;
        }
        return new Color(Part(0, 0), Part(1, 0), Part(2, 0), Part(3, 255));
    }

    // Keyed by absolute path so a folder reached through the tree and one reached through a
    // stored {game} token land on the same entry.
    static string Key(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path;
        }
    }
}
