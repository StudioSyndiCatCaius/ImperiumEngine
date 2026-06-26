namespace ImperiumCore.Classes;

public class ImpParse
{
    protected Dictionary<string, object?> data = new();

    // ------------------------------------------------------------------------------------------------------------
    // Statics
    // ------------------------------------------------------------------------------------------------------------

    public static ImpParse ParseFile(string file)
    {
        var content = File.ReadAllText(file);
        ImpParse parser = Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".json" => new Prase.parse_json(),
            ".toml" => new Prase.parse_toml(),
            ".csv"  => new Prase.parse_csv(),
            _       => new ImpParse()
        };
        parser.Parse(content);
        return parser;
    }

    public static ImpParse ParseString(string str)
    {
        ImpParse parser = Sniff(str);
        parser.Parse(str);
        return parser;
    }

    public static ImpParse ParseBytes(byte[] bytes)
    {
        return ParseString(System.Text.Encoding.UTF8.GetString(bytes));
    }

    private static ImpParse Sniff(string str)
    {
        var t = str.TrimStart();
        if (t.StartsWith('{') || t.StartsWith('[')) return new Prase.parse_json();
        if (t.Contains(',') && !t.Contains('=')) return new Prase.parse_csv();
        return new Prase.parse_toml();
    }

    // ------------------------------------------------------------------------------------------------------------
    // Parse (override per format)
    // ------------------------------------------------------------------------------------------------------------

    protected virtual void Parse(string content) { }

    // ------------------------------------------------------------------------------------------------------------
    // Table / key access
    // ------------------------------------------------------------------------------------------------------------

    public virtual IEnumerable<string> Keys => data.Keys;
    public bool HasField(string field)        => data.ContainsKey(field);
    public object? GetRaw(string field)       => data.TryGetValue(field, out var v) ? v : null;

    // Returns a sub-parser for a nested table (null if the key doesn't exist or isn't a table).
    public virtual ImpParse? GetTable(string field) => null;

    // ------------------------------------------------------------------------------------------------------------
    // Gets / Sets  (dotted keys traverse sub-tables)
    // ------------------------------------------------------------------------------------------------------------

    public T? GetField<T>(string field)
    {
        var dot = field.IndexOf('.');
        if (dot >= 0)
        {
            var sub = GetTable(field[..dot]);
            return sub != null ? sub.GetField<T>(field[(dot + 1)..]) : default;
        }

        if (!data.TryGetValue(field, out var val) || val is null) return default;
        if (val is T typed) return typed;
        try { return (T)Convert.ChangeType(val, typeof(T)); }
        catch { return default; }
    }

    public void SetField(string field, object? value)
    {
        var dot = field.IndexOf('.');
        if (dot >= 0)
        {
            var sub = GetTable(field[..dot]);
            if (sub != null) { sub.SetField(field[(dot + 1)..], value); return; }
        }
        data[field] = value;
    }
}
