namespace ImperiumCore.Structs;


public readonly struct TLabel : IEquatable<TLabel>
{
    private readonly string _value;
    private readonly int _hashCode;

    public TLabel(string value)
    {
        _value = value;
        _hashCode = value?.GetHashCode() ?? 0;
    }

    public static bool operator ==(TLabel left, TLabel right) => left._hashCode == right._hashCode && left._value == right._value;
    public static bool operator !=(TLabel left, TLabel right) => !(left == right);

    public override bool Equals(object obj) => obj is TLabel other && Equals(other);
    public bool Equals(TLabel other) => _hashCode == other._hashCode && _value == other._value;

    public override int GetHashCode() => _hashCode;
    public override string ToString() => _value;
}