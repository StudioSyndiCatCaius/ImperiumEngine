using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Dialogs;
using ImperiumEngine.Files;
using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

using System.Diagnostics.CodeAnalysis;
using System.Collections;


/// <summary>
/// Lightweight hierarchical tag
/// Example: "Status.Burning", "Weapon.Melee.Sword", "Ability.Fireball"
/// </summary>
public readonly struct TTag : IEquatable<TTag>, I_Property
{
    public static readonly TTag None = default;

    public string TagName { get; }

    public TTag(string tagName)
    {
        TagName = string.IsNullOrWhiteSpace(tagName) ? string.Empty : tagName.Trim();
        if (!string.IsNullOrEmpty(TagName))
        {
            ImpTags.Register(TagName, false);
        }
    }

    public bool IsValid => !string.IsNullOrEmpty(TagName);

    public string Leaf
    {
        get
        {
            if (string.IsNullOrEmpty(TagName))
            {
                return "";
            }
            int dot = TagName.LastIndexOf('.');
            if (dot < 0 || dot >= TagName.Length - 1)
            {
                return TagName;
            }
            return TagName.Substring(dot + 1);
        }
    }

    // Exact match
    public bool MatchesExact(TTag other)
    {
        return string.Equals(TagName, other.TagName, StringComparison.OrdinalIgnoreCase);
    }

    // Parent match (this tag is a parent of 'other' or exact)
    // "Weapon.Rifle".Matches(other: "Weapon.Rifle.Assault") → true
    public bool Matches(TTag other)
    {
        if (!IsValid || !other.IsValid)
        {
            return false;
        }
        if (string.Equals(TagName, other.TagName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return other.TagName.StartsWith(TagName + ".", StringComparison.OrdinalIgnoreCase);
    }

    // Child match (this tag is a child of 'other')
    public bool MatchesChild(TTag parent) => parent.Matches(this);

    public override string ToString() => TagName ?? "";
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(TagName ?? "");
    public bool Equals(TTag other) => string.Equals(TagName, other.TagName, StringComparison.OrdinalIgnoreCase);
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TTag t && Equals(t);

    public static bool operator ==(TTag a, TTag b) => a.Equals(b);
    public static bool operator !=(TTag a, TTag b) => !a.Equals(b);

    public static implicit operator TTag(string tag) => new(tag);

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty ui)
    {
        C2_Picker picker = new()
        {
            placeholder = "None",
            text_get = () =>
            {
                object v = ui.Value_Get();
                if (v is TTag t && t.IsValid)
                {
                    return t.TagName;
                }
                return "";
            },
            on_cleared = () => ui.Value_Set(TTag.None),
            on_open = () =>
            {
                TTag cur = TTag.None;
                if (ui.Value_Get() is TTag x)
                {
                    cur = x;
                }
                Dialog_TagPicker.Run(cur, picked => ui.Value_Set(picked),
                    title: "Select Tag",
                    allow_none: true);
            },
        };
        ui.Editor_Set(picker);
    }
}




public sealed class TTagSet : IEnumerable<TTag>, IEquatable<TTagSet>, I_Property
{
    private readonly HashSet<TTag> _tags = new();

    public TTagSet() { }

    public TTagSet(params TTag[] tags)
    {
        foreach (var t in tags)
            if (t.IsValid) _tags.Add(t);
    }

    public TTagSet(IEnumerable<TTag> tags)
    {
        foreach (var t in tags)
            if (t.IsValid) _tags.Add(t);
    }

    public int Count => _tags.Count;
    public bool IsEmpty => _tags.Count == 0;

    // ------------------------------------------------------------------
    // Mutation
    // ------------------------------------------------------------------

    public void AddTag(TTag tag)
    {
        if (tag.IsValid)
            _tags.Add(tag);
    }

    public void AddTags(TTagSet other)
    {
        if (other == null)
        {
            return;
        }
        foreach (var t in other._tags)
            _tags.Add(t);
    }

    public bool RemoveTag(TTag tag) => _tags.Remove(tag);

    public void RemoveTags(TTagSet other)
    {
        if (other == null)
        {
            return;
        }
        foreach (var t in other._tags)
            _tags.Remove(t);
    }

    public void Reset() => _tags.Clear();

    public TTagSet Clone() => new TTagSet(_tags);

    public List<TTag> Sorted()
    {
        List<TTag> list = new(_tags);
        list.Sort((a, b) => string.Compare(a.TagName, b.TagName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    // ------------------------------------------------------------------
    // Queries (the important Unreal-style methods)
    // ------------------------------------------------------------------

    /// <summary>Exact match only</summary>
    public bool HasTagExact(TTag tag) => _tags.Contains(tag);

    /// <summary>
    /// Hierarchical match (exact or parent).
    /// HasTag("Weapon.Rifle") returns true if container has "Weapon.Rifle.Assault"
    /// </summary>
    public bool HasTag(TTag tag)
    {
        if (!tag.IsValid) return false;

        foreach (var owned in _tags)
            if (tag.Matches(owned))
                return true;

        return false;
    }

    /// <summary>True if container has ANY of the tags in 'other' (hierarchical)</summary>
    public bool HasAny(TTagSet other)
    {
        if (other == null)
        {
            return false;
        }
        foreach (var t in other._tags)
            if (HasTag(t)) return true;
        return false;
    }

    /// <summary>True if container has ALL of the tags in 'other' (hierarchical)</summary>
    public bool HasAll(TTagSet other)
    {
        if (other == null)
        {
            return true;
        }
        foreach (var t in other._tags)
            if (!HasTag(t)) return false;
        return true;
    }

    /// <summary>Exact versions</summary>
    public bool HasAnyExact(TTagSet other)
    {
        if (other == null)
        {
            return false;
        }
        foreach (var t in other._tags)
            if (HasTagExact(t)) return true;
        return false;
    }

    public bool HasAllExact(TTagSet other)
    {
        if (other == null)
        {
            return true;
        }
        foreach (var t in other._tags)
            if (!HasTagExact(t)) return false;
        return true;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    public TTagSet GetGameplayTagParents()
    {
        var result = new TTagSet();
        foreach (var tag in _tags)
        {
            var parts = tag.TagName.Split('.');
            string current = parts[0];
            result.AddTag(current);

            for (int i = 1; i < parts.Length; i++)
            {
                current += "." + parts[i];
                result.AddTag(current);
            }
        }
        return result;
    }

    public override string ToString()
        => string.Join(", ", Sorted().Select(t => t.TagName));

    public IEnumerator<TTag> GetEnumerator() => _tags.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(TTagSet? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_tags.Count != other._tags.Count) return false;

        foreach (var t in _tags)
            if (!other._tags.Contains(t)) return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as TTagSet);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var t in Sorted()) hash.Add(t);
        return hash.ToHashCode();
    }

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty ui)
    {
        C2_TagSetEdit edit = new()
        {
            label = ui.label,
            value_get = () => ui.Value_Get() as TTagSet,
            value_set = set => ui.Value_Set(set),
        };
        ui.Editor_SetFull(edit);
    }
}


/// <summary>
/// Project-wide gameplay-tag table. Like UE DefaultGameplayTags.ini:
/// known tags live in {game}/Config/Tags.TOML and any TTag constructed this
/// session is merged in so the picker stays in sync with authored content.
/// </summary>
public static class ImpTags
{
    static readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    static bool _loaded;

    public static string FilePath()
    {
        string root = A_Game.game?.GetRootDir();
        if (string.IsNullOrWhiteSpace(root))
        {
            return "";
        }
        return Path.Combine(root, "Config", "Tags.TOML");
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        Load();
    }

    public static bool Contains(string name)
    {
        EnsureLoaded();
        name = Normalize(name);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        return _tags.Contains(name);
    }

    public static bool Contains(TTag tag)
    {
        if (!tag.IsValid)
        {
            return false;
        }
        return Contains(tag.TagName);
    }

    public static void Register(TTag tag, bool persist = false)
    {
        Register(tag.TagName, persist);
    }

    public static void Register(string name, bool persist = false)
    {
        EnsureLoaded();
        name = Normalize(name);
        if (string.IsNullOrEmpty(name) || !IsValid(name))
        {
            return;
        }
        _tags.Add(name);
        if (persist)
        {
            Save();
        }
    }

    public static bool Unregister(string name, bool persist = false)
    {
        EnsureLoaded();
        name = Normalize(name);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        bool removed = _tags.Remove(name);
        if (removed && persist)
        {
            Save();
        }
        return removed;
    }

    public static List<string> All()
    {
        EnsureLoaded();
        List<string> list = new(_tags);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }
        name = name.Trim();
        name = name.Replace('\\', '.');
        name = name.Replace('/', '.');
        while (name.Contains("..", StringComparison.Ordinal))
        {
            name = name.Replace("..", ".", StringComparison.Ordinal);
        }
        name = name.Trim('.');
        return name;
    }

    public static bool IsValid(string name)
    {
        name = Normalize(name);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        string[] parts = name.Split('.');
        if (parts.Length == 0)
        {
            return false;
        }
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                return false;
            }
            for (int c = 0; c < part.Length; c++)
            {
                char ch = part[c];
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
                {
                    continue;
                }
                return false;
            }
        }
        return true;
    }

    public static void Load()
    {
        string path = FilePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }
        File_TOML file = new() { filepath = path };
        if (!file.Parse())
        {
            return;
        }
        string[] names = file.doc.root.GetStrings("tags");
        if (names == null)
        {
            return;
        }
        for (int i = 0; i < names.Length; i++)
        {
            string n = Normalize(names[i]);
            if (IsValid(n))
            {
                _tags.Add(n);
            }
        }
    }

    public static void Save()
    {
        string path = FilePath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        List<string> names = All();
        TomlDoc doc = new();
        doc.root.Set("tags", names.ToArray());
        File_TOML file = new() { filepath = path, doc = doc };
        if (!file.WriteDoc())
        {
            Console.WriteLine("ImpTags.Save failed: " + path);
        }
    }
}
