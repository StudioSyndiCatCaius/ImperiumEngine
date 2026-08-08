using ImperiumEngine.Assets;

namespace ImperiumEngine.Main;

// Resolves a class's editor icon by name, walking up the inheritance chain: a type
// with no icon of its own falls back to the nearest ancestor that has one (eg. every
// ImpComp subclass without a dedicated icon shows the shared ImpComp.svg).
public static class ImpIcon
{
    const string DIR = "{engine}/Icons/Types/";

    static readonly Dictionary<Type, A_Texture?> cache = new();

    public static A_Texture? Get(Type type)
    {
        if (cache.TryGetValue(type, out var cached)) return cached;

        A_Texture? found = null;
        for (Type? t = type; t != null; t = t.BaseType)
        {
            string rel = DIR + t.Name + ".png";
            if (File.Exists(ImpPath.Path_Resolve(rel)))
            {
                found = new A_Texture { source_file = rel };
                break;
            }
        }

        cache[type] = found;
        return found;
    }

    // Icon for a bare name rather than a type. Inspector categories can carry a custom
    // label that corresponds to no class, so there is no hierarchy to walk here.
    public static A_Texture? Get(string name)
    {
        if (name.Length == 0) return null;
        if (cache_named.TryGetValue(name, out var cached)) return cached;

        string rel = DIR + name + ".png";
        var found = File.Exists(ImpPath.Path_Resolve(rel)) ? new A_Texture { source_file = rel } : null;

        cache_named[name] = found;
        return found;
    }

    static readonly Dictionary<string, A_Texture?> cache_named = new();

    public static A_Texture? Get(object instance) => Get(instance.GetType());
    public static A_Texture? Get<T>() => Get(typeof(T));
}
