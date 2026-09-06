using System.Reflection;

namespace Engine.Globals;

public static class GType
{
    // Loop over all subclasses of a given type.
    public static void ForEachOf(Type type, Action<Type, int> action, bool include_base=true, bool recursive=true)
    {
        if (type == null || action == null) return;
        int i = 0;
        if (include_base)
        {
            action(type, i);
            i++;
        }

        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            foreach (Type t in types)
            {
                if (t == null || t == type || !t.IsClass) continue;
                if (recursive)
                {
                    if (!type.IsAssignableFrom(t)) continue;
                }
                else if (t.BaseType != type) continue;
                action(t, i);
                i++;
            }
        }
    }
}
