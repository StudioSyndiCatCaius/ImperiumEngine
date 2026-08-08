using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

using System.Diagnostics.CodeAnalysis;
using System.Collections;


/// <summary>
/// Lightweight hierarchical tag 
/// Example: "Status.Burning", "Weapon.Melee.Sword", "Ability.Fireball"
/// </summary>
public readonly struct TTag : IEquatable<TTag>
{
    public static readonly TTag None = default;

    public string TagName { get; }

    public TTag(string tagName)
    {
        TagName = string.IsNullOrWhiteSpace(tagName) ? string.Empty : tagName;
    }

    public bool IsValid => !string.IsNullOrEmpty(TagName);

    // Exact match
    public bool MatchesExact(TTag other) => TagName == other.TagName;

    // Parent match (this tag is a parent of 'other' or exact)
    // "Weapon.Rifle".Matches(other: "Weapon.Rifle.Assault") → true
    public bool Matches(TTag other)
    {
        if (!IsValid || !other.IsValid) return false;
        if (TagName == other.TagName) return true;

        // Check if this is a parent of the other tag
        return other.TagName.StartsWith(TagName + ".", StringComparison.Ordinal);
    }

    // Child match (this tag is a child of 'other')
    public bool MatchesChild(TTag parent) => parent.Matches(this);

    public override string ToString() => TagName;
    public override int GetHashCode() => TagName.GetHashCode(StringComparison.Ordinal);
    public bool Equals(TTag other) => TagName == other.TagName;
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TTag t && Equals(t);

    public static bool operator ==(TTag a, TTag b) => a.Equals(b);
    public static bool operator !=(TTag a, TTag b) => !a.Equals(b);

    public static implicit operator TTag(string tag) => new(tag);
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
        foreach (var t in other._tags)
            _tags.Add(t);
    }

    public bool RemoveTag(TTag tag) => _tags.Remove(tag);

    public void RemoveTags(TTagSet other)
    {
        foreach (var t in other._tags)
            _tags.Remove(t);
    }

    public void Reset() => _tags.Clear();

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
        foreach (var t in other._tags)
            if (HasTag(t)) return true;
        return false;
    }

    /// <summary>True if container has ALL of the tags in 'other' (hierarchical)</summary>
    public bool HasAll(TTagSet other)
    {
        foreach (var t in other._tags)
            if (!HasTag(t)) return false;
        return true;
    }

    /// <summary>Exact versions</summary>
    public bool HasAnyExact(TTagSet other)
    {
        foreach (var t in other._tags)
            if (HasTagExact(t)) return true;
        return false;
    }

    public bool HasAllExact(TTagSet other)
    {
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
        => string.Join(", ", _tags.Select(t => t.TagName));

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
        foreach (var t in _tags) hash.Add(t);
        return hash.ToHashCode();
    }
}