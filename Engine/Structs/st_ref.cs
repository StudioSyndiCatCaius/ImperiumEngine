using ImperiumEngine.Classes;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Objects.Assets;
using Tomlyn.Model;

namespace ImperiumEngine.Structs;

// A typed reference to a *class* (not an instance) — the C# analogue of Unreal's TSubclassOf<T>.
// Holds either a Type guaranteed to be T or a concrete subclass of T, OR a user-created entity
// class: an .ImpEnt preset whose root component type is assignable to T (entities behave like
// user-defined subclasses of their root type). The editor renders it as a searchable dropdown of
// both, so only valid choices ever appear. Serializes to TOML via I_Serialize.
//
//   [ImpVar] public TRef<A_GameMode> game_mode;   // dropdown of A_GameMode and its subclasses
//   var mode = game_mode.New();                    // instantiate the chosen class / entity
public struct TRef<T> : I_Serialize, IEquatable<TRef<T>> where T : class
{
    // The keys used inside this reference's TOML sub-table (one or the other, never both).
    const string Key = "class";
    const string EntityKey = "entity";

    Type? _type;
    string _entity;   // keyword path to an .ImpEnt whose root type is assignable to T; "" = none

    public TRef(Type? type)
    {
        _type = IsValid(type) ? type : null;
        _entity = "";
    }

    // The referenced C# class, or null when unset / when an entity class is referenced instead.
    public readonly Type? Type => _type;

    // The keyword path of the referenced entity class, or "" when none.
    public readonly string EntityPath => _entity ?? "";

    // True when this reference points at a user-created entity class rather than a C# type.
    public readonly bool IsEntity => !string.IsNullOrEmpty(_entity);

    // The base/constraint type this reference is bound to (T).
    public static Type BaseType => typeof(T);

    // True when either a class or an entity is selected.
    public readonly bool HasValue => _type != null || !string.IsNullOrEmpty(_entity);

    // Instantiates the referenced class/entity, or returns null when unset. For an entity the
    // root component of the loaded preset is returned. Never throws for a valid C# selection.
    public readonly T? New()
    {
        if (_type != null) return (T?)Activator.CreateInstance(_type);
        if (!string.IsNullOrEmpty(_entity)) return NewFromEntity();
        return null;
    }

    readonly T? NewFromEntity()
    {
        var entity = new A_Entity();
        if (!entity.File_Load(ImpFile.Path_ToAbsolute(_entity))) return null;
        return entity.components.Count > 0 ? entity.components[0] as T : null;
    }

    public void Set(Type? type)
    {
        _type = IsValid(type) ? type : null;
        _entity = "";
    }

    // Points this reference at an entity class (an .ImpEnt keyword path). Clears any C# type.
    public void SetEntity(string path)
    {
        _entity = path ?? "";
        _type = null;
    }

    public void Clear()
    {
        _type = null;
        _entity = "";
    }

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
    // I_Serialize — a C# class stores just its name: field = { class = "A_GameMode" }; an entity
    // class stores its path: field = { entity = "{game}/Enemies/Goblin.ImpEnt" }. An unset
    // reference writes nothing, so it round-trips to null.
    // ------------------------------------------------------------------
    public readonly void File_WriteTo(TomlTable table)
    {
        if (_type != null) table[Key] = _type.Name;
        else if (!string.IsNullOrEmpty(_entity)) table[EntityKey] = _entity;
    }

    public void File_ReadFrom(TomlTable table)
    {
        if (table.TryGetValue(Key, out var raw) && raw is string name)
        {
            _type = Options().FirstOrDefault(t => t.Name == name);
            _entity = "";
        }
        else if (table.TryGetValue(EntityKey, out var e) && e is string path)
        {
            _entity = path;
            _type = null;
        }
    }

    public readonly bool Equals(TRef<T> other) => _type == other._type && EntityPath == other.EntityPath;
    public override readonly bool Equals(object? obj) => obj is TRef<T> o && Equals(o);
    public override readonly int GetHashCode() => HashCode.Combine(_type, EntityPath);
    public override readonly string ToString() =>
        _type?.Name ?? (IsEntity ? System.IO.Path.GetFileNameWithoutExtension(_entity) : "(none)");
}
