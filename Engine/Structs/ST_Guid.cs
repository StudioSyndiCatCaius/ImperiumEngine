using System.Globalization;
using System.Threading;
using Engine.Interfaces;

namespace Engine.Structs;

public struct TGuid32 : IEquatable<TGuid32>, I_Property
{
    static uint _next;

    [ImpVar] public uint value;

    public TGuid32(uint value) => this.value = value;

    public static TGuid32 None => default;
    public bool IsNone => value == 0;

    public static TGuid32 New()
    {
        uint v = Interlocked.Increment(ref _next);
        if (v == 0) v = Interlocked.Increment(ref _next);
        return new TGuid32(v);
    }

    public bool Equals(TGuid32 other) => value == other.value;
    public override bool Equals(object? obj) => obj is TGuid32 other && Equals(other);
    public override int GetHashCode() => unchecked((int)value);
    public override string ToString() => value.ToString("X8", CultureInfo.InvariantCulture);

    public static bool operator ==(TGuid32 a, TGuid32 b) => a.value == b.value;
    public static bool operator !=(TGuid32 a, TGuid32 b) => a.value != b.value;
    public static implicit operator uint(TGuid32 g) => g.value;
    public static implicit operator TGuid32(uint v) => new(v);

    public bool Property_IsCustomParse() => true;
    public void Property_Read(object raw)
    {
        switch (raw)
        {
            case string s when uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex):
                value = hex; break;
            case string s when uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint dec):
                value = dec; break;
            case int i: value = unchecked((uint)i); break;
            case long l: value = unchecked((uint)l); break;
            case uint u: value = u; break;
            default: value = 0; break;
        }
    }
    public object Property_Write() => ToString();
}

public struct TGuid64 : IEquatable<TGuid64>, I_Property
{
    static ulong _next;

    [ImpVar] public ulong value;

    public TGuid64(ulong value) => this.value = value;

    public static TGuid64 None => default;
    public bool IsNone => value == 0;

    public static TGuid64 New()
    {
        ulong v = Interlocked.Increment(ref _next);
        if (v == 0) v = Interlocked.Increment(ref _next);
        return new TGuid64(v);
    }

    public static void Seen(TGuid64 g)
    {
        if (g.value == 0) return;
        while (true)
        {
            ulong cur = Volatile.Read(ref _next);
            if (g.value <= cur) return;
            if (Interlocked.CompareExchange(ref _next, g.value, cur) == cur) return;
        }
    }

    public bool Equals(TGuid64 other) => value == other.value;
    public override bool Equals(object? obj) => obj is TGuid64 other && Equals(other);
    public override int GetHashCode() => value.GetHashCode();
    public override string ToString() => value.ToString("X16", CultureInfo.InvariantCulture);

    public static bool operator ==(TGuid64 a, TGuid64 b) => a.value == b.value;
    public static bool operator !=(TGuid64 a, TGuid64 b) => a.value != b.value;
    public static implicit operator ulong(TGuid64 g) => g.value;
    public static implicit operator TGuid64(ulong v) => new(v);

    public bool Property_IsCustomParse() => true;
    public void Property_Read(object raw)
    {
        switch (raw)
        {
            case string s when ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hex):
                value = hex; break;
            case string s when ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong dec):
                value = dec; break;
            case int i: value = unchecked((ulong)(uint)i); break;
            case long l: value = unchecked((ulong)l); break;
            case ulong u: value = u; break;
            default: value = 0; break;
        }
    }
    public object Property_Write() => ToString();
}
