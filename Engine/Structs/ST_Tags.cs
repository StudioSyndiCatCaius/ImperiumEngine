
using Engine.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace Engine.Structs;
public readonly struct TTag : IEquatable<TTag>, I_Property
{
    public bool Equals(TTag other)
    {
        throw new NotImplementedException();
    }

    public override bool Equals(object? obj)
    {
        return obj is TTag other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}




public sealed class TTagSet : IEnumerable<TTag>, IEquatable<TTagSet>, I_Property
{
    private readonly HashSet<TTag> _tags = new();

    
    public bool Has(TTag tag, bool exact=false) => _tags.Contains(tag);
    
    public bool HasAny(TTagSet tags, bool exact=false) => tags.Intersect(_tags).Any();
    public bool HasAll(TTagSet tags, bool exact=false) { return false;}

    public IEnumerator<TTag> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Equals(TTagSet? other)
    {
        throw new NotImplementedException();
    }
}

