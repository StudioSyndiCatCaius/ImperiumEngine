namespace ImperiumEngine.Structs;

public readonly struct TText
{
    private readonly string _key;
    private readonly object[] _args;
    //private static ResourceManager _resources;

    //public static void Init(ResourceManager rm) => _resources = rm;

    public TText(string key, params object[] args)
    {
        _key = key;
        _args = args;
    }

    public override string ToString()
    {
        //string format = _resources.GetString(_key, CultureInfo.CurrentUICulture) ?? _key;
        //return _args.Length > 0 ? string.Format(CultureInfo.CurrentUICulture, format, _args) : format;
        return _key;
    }
}

public readonly struct TLabel : IEquatable<TLabel>
{
    private static readonly Dictionary<string, int> _table = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> _entries = new();
    private static readonly object _lock = new();

    private readonly int _id;

    public TLabel(string value)
    {
        lock (_lock)
        {
            if (!_table.TryGetValue(value, out _id))
            {
                _id = _entries.Count;
                _entries.Add(value);
                _table[value] = _id;
            }
        }
    }

    public override string ToString() => _entries[_id];
    public bool Equals(TLabel other) => _id == other._id;
    public override bool Equals(object obj) => obj is TLabel n && Equals(n);
    public override int GetHashCode() => _id;
    public static bool operator ==(TLabel a, TLabel b) => a.Equals(b);
    public static bool operator !=(TLabel a, TLabel b) => !a.Equals(b);
}