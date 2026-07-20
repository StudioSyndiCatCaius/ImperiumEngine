using ImperiumEngine.Interfaces;
using Tomlyn.Model;

namespace ImperiumEngine.Structs;

// A typed reference to a *class* (not an instance) — the C# analogue of Unreal's TSubclassOf<T>.
// Holds a Type guaranteed to be T or a concrete subclass of T. The editor renders it as a
// dropdown listing every non-abstract class assignable to T (including T itself when concrete),
// so only valid choices ever appear. Serializes to TOML as its class name via I_Serialize.
//
//   [ImpVar] public TRef<A_GameMode> game_mode;   // dropdown of A_GameMode and its subclasses
//   var mode = game_mode.New();                    // instantiate the chosen class
public struct TRef<T> : I_Serialize, IEquatable<TRef<T>> where T : class
{
    // The key used inside this reference's TOML sub-table.
    const string Key = "class";

    Type? _type;

    public TRef(Type? type) => _type = IsValid(type) ? type : null;

    // The referenced class, or null when unset.
    public readonly Type? Type => _type;

    // The base/constraint type this reference is bound to (T).
    public static Type BaseType => typeof(T);

    // True when a class is selected.
    public readonly bool HasValue => _type != null;

    // Instantiates the referenced class, or returns null when unset. Never throws for a
    // valid selection: TRef only ever holds default-constructible types (see IsValid).
    public readonly T? New() => _type == null ? null : (T?)Activator.CreateInstance(_type);

    public void Set(Type? type) => _type = IsValid(type) ? type : null;

    // A legal target is T or a subclass, non-abstract, and default-constructible (so New() works).
    static bool IsValid(Type? t) =>
        t != null && typeof(T).IsAssignableFrom(t) && !t.IsAbstract &&
        t.GetConstructor(System.Type.EmptyTypes) != null;

    // Every concrete class assignable to T — the source for the editor dropdown.
    // Scans T's own assembly, matching the engine's other type-discovery helpers.
    public static IEnumerable<Type> Options()
    {
        foreach (var t in typeof(T).Assembly.GetTypes())
            if (IsValid(t)) yield return t;
    }

    // ------------------------------------------------------------------
    // I_Serialize — stored as just the class name: field = { class = "A_GameMode" }.
    // An unset reference writes nothing, so it round-trips to null.
    // ------------------------------------------------------------------
    public readonly void File_WriteTo(TomlTable table)
    {
        if (_type != null) table[Key] = _type.Name;
    }

    public void File_ReadFrom(TomlTable table)
    {
        if (table.TryGetValue(Key, out var raw) && raw is string name)
            _type = Options().FirstOrDefault(t => t.Name == name);
    }

    public readonly bool Equals(TRef<T> other) => _type == other._type;
    public override readonly bool Equals(object? obj) => obj is TRef<T> o && Equals(o);
    public override readonly int GetHashCode() => _type?.GetHashCode() ?? 0;
    public override readonly string ToString() => _type?.Name ?? "(none)";
}
