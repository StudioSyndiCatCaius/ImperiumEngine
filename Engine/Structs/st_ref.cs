namespace ImperiumEngine.Structs;

public struct TRef<T> // a soft reference to an on-disk asset
{
    public T Value { get; set; }

    public TRef(T value)
    {
        Value = value;
    }
}


public struct TClass<T> // a reference to a class (usually an Imp Class)
{
    public T Value { get; set; }

    public TClass(T value)
    {
        Value = value;
    }
}
