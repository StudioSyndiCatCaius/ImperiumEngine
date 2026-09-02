using System.Globalization;
using System.Runtime.CompilerServices;
using Engine.Interfaces;
using Engine.Structs;

/// <summary>
/// Lightweight TText-style text handle for the engine.
/// Prefer this over raw strings when the text can be localized or formatted.
/// </summary>
public struct TText : IEquatable<TText>, IFormattable , I_Property
{
    // ------------------------------------------------------------------
    // Storage
    // ------------------------------------------------------------------

    private readonly string? _key;          // localization key (null = pure string)
    private readonly string? _fallback;     // source / fallback text
    private readonly object[]? _args;       // format arguments (null = no formatting)

    public string? Key => _key;
    public string Fallback => _fallback ?? string.Empty;
    public bool IsLocalized => !string.IsNullOrEmpty(_key);
    public bool IsFormatted => _args is { Length: > 0 };

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    public TText(string text)
    {
        _key = null;
        _fallback = text;
        _args = null;
    }

    public TText(string key, string fallback)
    {
        _key = key;
        _fallback = fallback;
        _args = null;
    }

    private TText(string? key, string? fallback, object[]? args)
    {
        _key = key;
        _fallback = fallback;
        _args = args;
    }

    // ------------------------------------------------------------------
    // Factory helpers (Unreal-style)
    // ------------------------------------------------------------------

    public static TText FromString(string text) => new(text);

    public static TText FromStringTable(string key, string fallback) => new(key, fallback);

    /// <summary>
    /// Format like TText::Format
    /// Example: TText.Format("Hello {0}, you have {1} gold", name, gold)
    /// </summary>
    public static TText Format(string format, params object[] args)
        => new(null, format, args);

    public static TText Format(TText format, params object[] args)
        => new(format._key, format._fallback, args);

    // ------------------------------------------------------------------
    // Resolution
    // ------------------------------------------------------------------

    /// <summary>
    /// Resolves the final string.
    /// Later you can plug a real localization system here.
    /// </summary>
    public string Resolve(IFormatProvider? provider = null)
    {
        // TODO: replace this with your real localization lookup
        // string? localized = LocalizationManager.Get(_key);
        // if (localized != null) ...

        string source = _fallback ?? string.Empty;

        if (_args is { Length: > 0 })
            return string.Format(provider ?? CultureInfo.CurrentCulture, source, _args);

        return source;
    }

    // ------------------------------------------------------------------
    // Implicit / explicit conversions
    // ------------------------------------------------------------------

    public static implicit operator TText(string text) => new(text);
    public static explicit operator string(TText text) => text.Resolve();

    // ------------------------------------------------------------------
    // Equality & hashing
    // ------------------------------------------------------------------

    public bool Equals(TText other)
        => _key == other._key
        && _fallback == other._fallback
        && ArgsEqual(_args, other._args);

    public override bool Equals(object? obj) => obj is TText other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_key);
        hash.Add(_fallback);
        if (_args != null)
            foreach (var a in _args)
                hash.Add(a);
        return hash.ToHashCode();
    }

    public static bool operator ==(TText left, TText right) => left.Equals(right);
    public static bool operator !=(TText left, TText right) => !left.Equals(right);

    // ------------------------------------------------------------------
    // ToString / IFormattable
    // ------------------------------------------------------------------

    public override string ToString() => Resolve();

    public string ToString(string? format, IFormatProvider? formatProvider)
        => Resolve(formatProvider);

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static bool ArgsEqual(object[]? a, object[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.Length != b.Length) return false;

        for (int i = 0; i < a.Length; i++)
            if (!Equals(a[i], b[i])) return false;

        return true;
    }

    // Empty instance (like TText::GetEmpty())
    public static readonly TText Empty = new(string.Empty);

    public TText With(string? key, string? fallback)
        => new(string.IsNullOrEmpty(key) ? null : key, fallback, _args);

    // ------------------------------------------------------------------
    // I_Property
    // ------------------------------------------------------------------

    public bool Property_IsCustomParse() => true;

    public void Property_Read(object raw) => this = Parse(raw);

    public object Property_Write()
    {
        if (string.IsNullOrEmpty(_key))
            return _fallback ?? "";
        TTable table = new();
        table.Set("key", _key);
        table.Set("text", _fallback ?? "");
        return table;
    }

    public static TText Parse(object raw)
    {
        if (raw is TTable table)
        {
            string key = table.get_String("key");
            string text = table.get_String("text");
            return string.IsNullOrEmpty(key) ? new TText(text) : new TText(key, text);
        }
        return new TText(raw as string ?? raw?.ToString() ?? "");
    }
}