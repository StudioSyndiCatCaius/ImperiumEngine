namespace ImperiumCore.Structs;

public readonly struct TClass<TBase> where TBase : class
{
    private readonly System.Type _type;

    public System.Type Type => _type;

    public TClass(Type type)
    {
        if (type != null && !typeof(TBase).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type} must derive from {typeof(TBase)}");

        _type = type;
    }

    // Implicit conversion from Type - very convenient
    public static implicit operator TClass<TBase>(Type type)
        => new TClass<TBase>(type);

    // Implicit conversion back to Type
    public static implicit operator Type(TClass<TBase> wrapper)
        => wrapper._type;

    // Helper methods
    public bool IsValid => _type != null;
    public bool IsSubclassOf<T>() where T : TBase => _type == typeof(T) || _type?.IsSubclassOf(typeof(T)) == true;
}