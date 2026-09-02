using Engine.Enums;

namespace Engine.Core;

/*
 *  Sandbox is a handler for scripting. 2 types to start with:
 *  - Lua
 *  - Vis - visual scripting (will implement fully later)
 *
 * -- Attributes--
 * [ScriptCall] can be called in the scripting system.
 * [ScriptHook] can add a name-matching function in the script to run code when this function is called (E.G. OnBegin, OnUpdate, OnEnd)
 *
 */
public class TScriptValue
{
    public ImpSandbox? sandbox;
    public ImpComp? owner;
    public object? handle;

    public bool Has(string name)
    {
        return sandbox != null && sandbox.Has(this, name);
    }

    public void Call(string name, params object[] args)
    {
        sandbox?.Call(this, name, args);
    }
}

public abstract class ImpSandbox
{
    public static ImpSandbox? current;

    public abstract void Init();
    public abstract void Shutdown();
    public abstract void RunGlobal(string path);
    public abstract TScriptValue? RunInstance(string path, ImpComp owner);
    public abstract bool Has(TScriptValue inst, string name);
    public abstract void Call(TScriptValue inst, string name, params object[] args);

    public void Bind(ImpComp c)
    {
        if (c == null || c.script_instance != null) return;
        string? path = SidecarPath(c);
        if (path == null) return;
        TScriptValue? inst = RunInstance(path, c);
        if (inst != null) c.script_instance = inst;
    }

    public static string? SidecarPath(ImpComp c)
    {
        string? local = null;
        if (c.is_prefab && c.prefab_scene != null && !string.IsNullOrEmpty(c.prefab_scene.filepath))
            local = Path.ChangeExtension(c.prefab_scene.filepath, ".lua");
        else if (c.parent == null && c.scene != null && !string.IsNullOrEmpty(c.scene.filepath))
            local = Path.ChangeExtension(c.scene.filepath, ".lua");
        if (string.IsNullOrEmpty(local)) return null;
        string abs = Imp.Make_Path_Absolute(local);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) return null;
        return abs;
    }

    public static void LoadGlobals()
    {
        if (current == null) return;
        string root = Imp.GetDir_Root(EContentDir.Game);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

        string g = Path.Combine(root, "G.lua");
        if (File.Exists(g)) current.RunGlobal(g);

        string mods = Path.Combine(root, "Mods");
        if (!Directory.Exists(mods)) return;
        foreach (string dir in Directory.GetDirectories(mods))
        {
            string mg = Path.Combine(dir, "G.lua");
            if (File.Exists(mg)) current.RunGlobal(mg);
        }
    }
}
