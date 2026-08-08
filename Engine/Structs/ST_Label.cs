using System.Collections.Concurrent;

namespace ImperiumEngine.Structs;

public readonly struct TLabel : IEquatable<TLabel>
{
    // ---------------------------------------------
    // Instance
    // ---------------------------------------------

    public static readonly TLabel None = default;

    private readonly int _index;

    private TLabel(int index) => _index = index;
    
    public string Value => Get(_index);
    public bool IsNone  => _index == 0;

    // ---------------------------------------------
    // Creation
    // ---------------------------------------------

    public static TLabel From(string? name) => new(FindOrAdd(name));

    public static implicit operator TLabel(string? name) => From(name);
    public static implicit operator string(TLabel label) => label.Value;

    // ---------------------------------------------
    // Equality (just an int compare)
    // ---------------------------------------------

    public bool Equals(TLabel other) => _index == other._index;
    public override bool Equals(object? obj) => obj is TLabel other && Equals(other);
    public override int GetHashCode() => _index;

    public static bool operator ==(TLabel a, TLabel b) => a._index == b._index;
    public static bool operator !=(TLabel a, TLabel b) => a._index != b._index;

    public override string ToString() => Value;

    // ---------------------------------------------
    // Internal name table (all static, lives inside the struct)
    // ---------------------------------------------

    private static readonly ConcurrentDictionary<string, int> Map =
        new(StringComparer.Ordinal);                    // change to OrdinalIgnoreCase if you want

    private static readonly List<string> Names = new() { string.Empty }; // 0 = None
    private static readonly object Lock = new();

    private static int FindOrAdd(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;

        if (Map.TryGetValue(name, out int index))
            return index;

        lock (Lock)
        {
            if (Map.TryGetValue(name, out index))
                return index;

            index = Names.Count;
            Names.Add(name);
            Map[name] = index;
            return index;
        }
    }

    private static string Get(int index)
    {
        if ((uint)index >= (uint)Names.Count)
            return string.Empty;

        return Names[index];
    }
}