using System.Collections;
using System.Reflection;
using System.Text;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Classes;

public class ImpFile
{
    // Set once at startup by the engine entry point
    public static string s_projectDir = "";
    public static string s_engineContentDir = "";


    /*
     * Resolve path keywords to absolute paths:
     *      {engine} / {Engine} : the Engine "Content" directory
     *      {editor} / {Editor} : the Engine "Content" directory
     *      {game}              : the Game/Project's "Content" directory.
     *          NOTE: if `CFG_Modding.modding_enabled` is true, will load from the game's Mod directory by matching file path
     *          Mod files are ONLY loaded at runtime, NOT editor. Editor will always use native game Content assets.
     */
    public static string Path_ToAbsolute(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        return source
            .Replace("{game}",   Path.Combine(s_projectDir, "Content"))
            .Replace("{engine}", s_engineContentDir)
            .Replace("{Engine}", s_engineContentDir)
            .Replace("{editor}", s_engineContentDir)
            .Replace("{Editor}", s_engineContentDir);
    }
    
    // Inverse of ResolvePath: rewrites an absolute path back into keyword form so stored
    // references stay portable across machines. Unmatched paths are returned normalized.
    public static string Path_ToRelative(string absolute)
    {
        if (string.IsNullOrEmpty(absolute)) return absolute;
        string norm   = absolute.Replace('\\', '/');
        string game   = Path.Combine(s_projectDir, "Content").Replace('\\', '/');
        string engine = s_engineContentDir.Replace('\\', '/');

        if (!string.IsNullOrEmpty(game) && norm.StartsWith(game, StringComparison.OrdinalIgnoreCase))
            return "{game}" + norm[game.Length..];
        if (!string.IsNullOrEmpty(engine) && norm.StartsWith(engine, StringComparison.OrdinalIgnoreCase))
            return "{engine}" + norm[engine.Length..];
        return norm;
    }

    // True if `path` is the same as, or lives under, `dir` (both treated as absolute paths).
    public static bool Path_IsUnder(string path, string dir)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(dir)) return false;
        try
        {
            string p = Path.GetFullPath(path);
            string d = Path.GetFullPath(dir);
            if (string.Equals(p, d, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = d.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;
            return p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // Remaps an absolute path that lives under `oldDir` into the equivalent path under `newDir`.
    // Paths outside `oldDir` are returned unchanged.
    public static string Path_Remap(string path, string oldDir, string newDir)
    {
        if (string.IsNullOrEmpty(path) || !Path_IsUnder(path, oldDir)) return path;
        try
        {
            string p = Path.GetFullPath(path);
            string o = Path.GetFullPath(oldDir);
            string n = Path.GetFullPath(newDir);
            string rel = Path.GetRelativePath(o, p);
            return rel == "." ? n : Path.GetFullPath(Path.Combine(n, rel));
        }
        catch { return path; }
    }

    // After a file or folder is moved/renamed: rewrites keyword-form (and absolute) path prefixes
    // in project/engine text files so asset references, _source links, last_level, etc. keep working.
    // Call AFTER the physical move. Returns the number of files that were modified on disk.
    public static int RewritePathReferences(string oldPath, string newPath)
    {
        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath)) return 0;

        string oldAbs, newAbs;
        try
        {
            oldAbs = Path.GetFullPath(oldPath);
            newAbs = Path.GetFullPath(newPath);
        }
        catch { return 0; }

        if (string.Equals(oldAbs, newAbs, StringComparison.OrdinalIgnoreCase)) return 0;

        // forms we may find in TOML / config text
        var pairs = new List<(string old, string neu)>();
        void AddPair(string o, string n)
        {
            if (string.IsNullOrEmpty(o) || string.Equals(o, n, StringComparison.Ordinal)) return;
            pairs.Add((o, n));
        }

        AddPair(Path_ToRelative(oldAbs).Replace('\\', '/'), Path_ToRelative(newAbs).Replace('\\', '/'));
        AddPair(oldAbs.Replace('\\', '/'), newAbs.Replace('\\', '/'));
        AddPair(oldAbs, newAbs);

        // also try {Engine} capitalisation variant for engine-root paths
        string oldKw = Path_ToRelative(oldAbs).Replace('\\', '/');
        string newKw = Path_ToRelative(newAbs).Replace('\\', '/');
        if (oldKw.StartsWith("{engine}", StringComparison.OrdinalIgnoreCase))
            AddPair("{Engine}" + oldKw["{engine}".Length..],
                    "{Engine}" + newKw["{engine}".Length..]);

        var roots = new List<string>();
        string gameContent = Path.Combine(s_projectDir, "Content");
        string gameConfig  = Path.Combine(s_projectDir, "Config");
        if (Directory.Exists(gameContent)) roots.Add(gameContent);
        if (Directory.Exists(gameConfig))  roots.Add(gameConfig);
        if (!string.IsNullOrEmpty(s_engineContentDir) && Directory.Exists(s_engineContentDir))
            roots.Add(s_engineContentDir);

        int count = 0;
        foreach (string root in roots)
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (string file in files)
            {
                if (!IsRewritableTextFile(file)) continue;
                try
                {
                    string text = File.ReadAllText(file);
                    string updated = text;
                    foreach (var (o, n) in pairs)
                        updated = RewritePrefix(updated, o, n);
                    if (updated != text)
                    {
                        File.WriteAllText(file, updated);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ImpFile] Could not rewrite refs in {file}: {ex.Message}");
                }
            }
        }
        return count;
    }

    // Walks an in-memory object graph (open documents) and remaps ImpAsset.file_link / file_source
    // that live under oldDir. Covers nested [ImpVar] asset slots and component trees.
    public static void RemapAssetPathsInMemory(object? root, string oldDir, string newDir)
    {
        if (root == null || string.IsNullOrEmpty(oldDir)) return;
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        WalkRemap(root, oldDir, newDir, seen);
    }

    static void WalkRemap(object? obj, string oldDir, string newDir, HashSet<object> seen)
    {
        if (obj == null) return;
        var type = obj.GetType();
        if (type.IsValueType || obj is string) return;
        if (!seen.Add(obj)) return;

        if (obj is ImpAsset asset)
        {
            if (!string.IsNullOrEmpty(asset.file_link) && Path_IsUnder(asset.file_link, oldDir))
                asset.file_link = Path_Remap(asset.file_link, oldDir, newDir);
            if (!string.IsNullOrEmpty(asset.file_source) && Path_IsUnder(asset.file_source, oldDir))
                asset.file_source = Path_Remap(asset.file_source, oldDir, newDir);

            if (asset is A_Entity entity)
                foreach (var c in entity.components)
                    WalkRemap(c, oldDir, newDir, seen);
        }

        if (obj is ImpComponent component)
            foreach (var child in component.Children)
                WalkRemap(child, oldDir, newDir, seen);

        // [ImpVar] fields: nested assets, components, lists, dicts
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!field.IsDefined(typeof(ImpVarAttribute), false)) continue;
                var value = field.GetValue(obj);
                if (value == null || value is string || value.GetType().IsValueType) continue;

                if (value is IDictionary dict)
                {
                    foreach (var v in dict.Values) WalkRemap(v, oldDir, newDir, seen);
                }
                else if (value is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) WalkRemap(item, oldDir, newDir, seen);
                }
                else
                {
                    WalkRemap(value, oldDir, newDir, seen);
                }
            }
        }
    }

    static bool IsRewritableTextFile(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".impasset", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".Implvl", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ImpLvl", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ImpEnt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ImpGame", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".toml", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    // Case-insensitive prefix rewrite that only matches at a path boundary
    // (end of string, slash, quote, whitespace, etc.) so "{game}/A/2D" does not
    // clobber "{game}/A/2DExtra".
    static string RewritePrefix(string text, string oldPrefix, string newPrefix)
    {
        if (string.IsNullOrEmpty(oldPrefix) || string.Equals(oldPrefix, newPrefix, StringComparison.Ordinal))
            return text;

        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            int idx = text.IndexOf(oldPrefix, i, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            sb.Append(text, i, idx - i);
            int after = idx + oldPrefix.Length;
            bool boundary = after >= text.Length || text[after] is
                '/' or '\\' or '"' or '\'' or '\r' or '\n' or ' ' or '\t' or
                ',' or ']' or '}' or ')' or '>';
            sb.Append(boundary ? newPrefix : text.AsSpan(idx, oldPrefix.Length));
            i = after;
        }
        return sb.ToString();
    }
}
