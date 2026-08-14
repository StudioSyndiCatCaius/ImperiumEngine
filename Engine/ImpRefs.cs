using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImperiumEngine.Assets;
using ImperiumEngine.Files;
using ImperiumEngine.Structs;

namespace ImperiumEngine;

/// <summary>
/// Repoints asset references after a file or folder is renamed or moved.
///
/// A reference is a path string living inside a .Imp* file, written in one of three styles:
/// tokenized ("{game}/Foo.ImpAsset"), relative to the file holding it ("foo.png"), or absolute.
/// Each one is rewritten in the style it was already written in, so a rename never quietly
/// turns a portable path into a machine-specific one.
///
/// Anything already in memory is fixed too - otherwise saving an open scene afterwards would
/// write the pre-rename path straight back over the repaired file.
/// </summary>
public static class ImpRefs
{
    static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Rewrites every reference to old_path (and to anything under it, so a folder carries its
    /// contents) so it points at new_path. Returns the number of files rewritten on disk.
    /// Call after the move has happened, so relative paths resolve against where files now live.
    /// </summary>
    public static int Repath(string old_path, string new_path)
    {
        if (string.IsNullOrEmpty(old_path) || string.IsNullOrEmpty(new_path)) return 0;

        string from, to;
        try
        {
            from = Path_Norm(old_path);
            to = Path_Norm(new_path);
        }
        catch { return 0; }
        //An ordinal compare, not a path compare: renaming "foo" to "Foo" is a real rename.
        if (string.Equals(from, to, StringComparison.Ordinal)) return 0;

        Memory_Repath(from, to);
        return Disk_Repath(from, to);
    }

    static string Path_Norm(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    // -------------------------------------------------------------------
    // Disk
    // -------------------------------------------------------------------

    static int Disk_Repath(string from, string to)
    {
        int changed = 0;
        foreach (string file in Files_All())
        {
            JsonNode? root;
            try { root = JsonNode.Parse(File.ReadAllText(file)); }
            catch { continue; }
            if (root == null) continue;

            if (!Node_Repath(root, file, from, to)) continue;
            try { File.WriteAllText(file, root.ToJsonString(WriteOptions)); }
            catch { continue; }
            changed++;
        }
        return changed;
    }

    /// <summary>Every .Imp* file that could be holding a reference, both content roots plus the game file.</summary>
    static List<string> Files_All()
    {
        List<string> found = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        void Add(string path)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch { full = path; }
            if (seen.Add(full)) found.Add(full);
        }

        // The game and engine roots collapse to the same folder when no project is loaded.
        foreach (string root in new[] { ImpFile.ContentDir_Game(), ImpFile.ContentDir_Engine() })
        {
            if (!Directory.Exists(root)) continue;
            string[] files;
            try { files = Directory.GetFiles(root, "*.Imp*", SearchOption.AllDirectories); }
            catch { continue; }
            foreach (string file in files) Add(file);
        }

        //The .ImpGame sits beside Content rather than inside it, so the sweep above misses it.
        string? game_file = A_Game.game?.filepath;
        if (!string.IsNullOrEmpty(game_file) && !ImpAsset.Path_IsBuiltin(game_file))
        {
            string real = ImpFile.Path_Resolve(game_file);
            if (File.Exists(real)) Add(real);
        }

        return found;
    }

    static bool Node_Repath(JsonNode node, string file, string from, string to)
    {
        bool changed = false;

        switch (node)
        {
            case JsonObject obj:
            {
                List<string> keys = new();
                foreach (KeyValuePair<string, JsonNode?> kv in obj) keys.Add(kv.Key);
                foreach (string key in keys)
                {
                    JsonNode? child = obj[key];
                    if (child == null) continue;
                    if (String_Of(child) is string s)
                    {
                        string? moved = Ref_Rewrite(s, file, from, to);
                        if (moved == null) continue;
                        obj[key] = JsonValue.Create(moved);
                        changed = true;
                        continue;
                    }
                    changed |= Node_Repath(child, file, from, to);
                }
                break;
            }
            case JsonArray arr:
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    JsonNode? child = arr[i];
                    if (child == null) continue;
                    if (String_Of(child) is string s)
                    {
                        string? moved = Ref_Rewrite(s, file, from, to);
                        if (moved == null) continue;
                        arr[i] = JsonValue.Create(moved);
                        changed = true;
                        continue;
                    }
                    changed |= Node_Repath(child, file, from, to);
                }
                break;
            }
        }

        return changed;
    }

    static string? String_Of(JsonNode node)
    {
        return node is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;
    }

    // -------------------------------------------------------------------
    // Reference strings
    // -------------------------------------------------------------------

    /// <summary>
    /// The rewritten form of a reference string, or null when it is not a reference to the
    /// moved path. holder is the file the string was read from, and may be null in memory.
    /// </summary>
    static string? Ref_Rewrite(string? value, string? holder, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (ImpAsset.Path_IsBuiltin(value)) return null;

        // A bare word is a class name, a label or a colour, not a path. Only strings carrying a
        // token, a separator or an extension are worth resolving.
        bool tokenized = value.Contains("{engine}") || value.Contains("{game}");
        bool has_sep = value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0;
        if (!tokenized && !has_sep && string.IsNullOrEmpty(Path.GetExtension(value))) return null;

        string full;
        try { full = Path_Norm(File_JSON.Path_ResolveRef(holder, value)); }
        catch { return null; }

        string moved;
        if (string.Equals(full, from, StringComparison.OrdinalIgnoreCase)) moved = to;
        else if (full.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            moved = to + full[from.Length..];
        else return null;

        string written = Ref_Write(moved, holder, tokenized, Path.IsPathRooted(value));
        return string.Equals(written, value, StringComparison.Ordinal) ? null : written;
    }

    static string Ref_Write(string full, string? holder, bool tokenized, bool rooted)
    {
        if (tokenized) return File_JSON.Path_Tokenize(full);
        if (rooted || string.IsNullOrEmpty(holder)) return full;

        string? dir = Path.GetDirectoryName(ImpFile.Path_Resolve(holder));
        if (string.IsNullOrEmpty(dir)) return full;
        try
        {
            string rel = Path.GetRelativePath(dir, full);
            return string.IsNullOrEmpty(rel) ? full : rel.Replace('\\', '/');
        }
        catch { return full; }
    }

    // -------------------------------------------------------------------
    // Memory
    // -------------------------------------------------------------------

    // ImpAsset.Cache_Rekey / ImpFile.Cache_Rekey already carry the loaded objects across, which
    // covers every reference held as an object. What they cannot reach is TRef, which holds its
    // target as a path string and only resolves it on demand - so those are walked by hand.
    static void Memory_Repath(string from, string to)
    {
        HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
        foreach (ImpAsset asset in ImpAsset.Loaded_GetAll().ToList())
            Value_Repath(asset, from, to, seen, 0);
        Value_Repath(ImpScene.current, from, to, seen, 0);
        Value_Repath(ImpScene.global, from, to, seen, 0);
    }

    /// <summary>
    /// Walks value looking for TRef paths. Returns the value to store back when it is a struct
    /// that changed (a boxed copy the caller has to assign), otherwise null.
    /// </summary>
    static object? Value_Repath(object? value, string from, string to, HashSet<object> seen, int depth)
    {
        if (value == null || depth > 32) return null;
        Type type = value.GetType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TRef<>))
        {
            FieldInfo? path_field = type.GetField("path", BindingFlags.Public | BindingFlags.Instance);
            if (path_field == null) return null;
            string? moved = Ref_Rewrite(path_field.GetValue(value) as string, null, from, to);
            if (moved == null) return null;
            path_field.SetValue(value, moved);
            return value;
        }

        if (!Type_IsWalkable(type)) return null;
        if (!type.IsValueType && !seen.Add(value)) return null;

        if (value is IList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                object? moved = Value_Repath(list[i], from, to, seen, depth + 1);
                if (moved != null && !list.IsReadOnly) list[i] = moved;
            }
            return null;
        }

        if (value is IDictionary dict)
        {
            List<object> keys = new();
            foreach (object key in dict.Keys) keys.Add(key);
            foreach (object key in keys)
            {
                object? moved = Value_Repath(dict[key], from, to, seen, depth + 1);
                if (moved != null) dict[key] = moved;
            }
            return null;
        }

        bool changed = false;
        foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (f.IsLiteral) continue;
            object? current;
            try { current = f.GetValue(value); }
            catch { continue; }

            object? moved = Value_Repath(current, from, to, seen, depth + 1);
            //A readonly field still gets walked - only writing the struct copy back is off limits.
            if (moved == null || f.IsInitOnly) continue;
            try { f.SetValue(value, moved); changed = true; }
            catch { }
        }

        return type.IsValueType && changed ? value : null;
    }

    // Keeps the walk inside our own object graph. Without this it would wander into Raylib
    // handles and framework internals looking for a path string that cannot be there.
    static bool Type_IsWalkable(Type? type)
    {
        if (type == null) return false;
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) return false;
        if (type.IsArray) return Type_IsWalkable(type.GetElementType());
        if (type.IsGenericType)
        {
            Type gen = type.GetGenericTypeDefinition();
            if (gen == typeof(List<>) || gen == typeof(Dictionary<,>))
            {
                foreach (Type arg in type.GetGenericArguments())
                    if (Type_IsWalkable(arg)) return true;
                return false;
            }
        }
        string? ns = type.Namespace;
        if (ns == null) return false;
        return ns.StartsWith("ImperiumEngine", StringComparison.Ordinal)
               || ns.StartsWith("Editor", StringComparison.Ordinal);
    }
}
