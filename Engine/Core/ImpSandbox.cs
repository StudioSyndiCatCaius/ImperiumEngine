using Engine.Assets;
using Engine.Enums;
using Engine.Globals;

namespace Engine.Core;

/*
 * Scripting sandbox. Two backends later:
 *  - C# scripts
 *  - Vis (visual scripting)
 *
 * [ScriptCall]  callable from a sandbox
 * [ScriptHook]  overridable from a sandbox (OnBegin, OnUpdate, OnEnd, ...)
 *
 * Nothing is bound or ticked until a sandbox is actually installed.
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

    public void Call(string name)
    {
        sandbox?.Call(this, name);
    }

    public void Tick(double dt)
    {
        sandbox?.Tick(this, dt);
    }
}

public abstract class ImpSandbox
{
    public static ImpSandbox? current;

    public abstract void Init();
    public abstract void Shutdown();
    public abstract void RunGlobal(string path);
    public abstract TScriptValue? RunInstance(string path, ImpComp owner);
    public virtual TScriptValue? RunInstance(A_Script script, ImpComp owner) => null;
    public abstract bool Has(TScriptValue inst, string name);
    public abstract void Call(TScriptValue inst, string name);
    public virtual void Tick(TScriptValue inst, double dt) { }

    public void Bind(ImpComp c)
    {
        if (c == null || c.script_instance != null) return;
        if (c.parent != null) return;
        A_Script? script = c.scene?.script;
        if (script == null || script.nodes.Count == 0) return;
        TScriptValue? inst = RunInstance(script, c);
        if (inst != null) c.script_instance = inst;
    }

    public static string? SidecarPath(ImpComp c)
    {
        if (c == null) return null;
        string? local = null;
        if (c.is_prefab && c.prefab_scene != null && !string.IsNullOrEmpty(c.prefab_scene.filepath))
            local = c.prefab_scene.filepath;
        else if (c.parent == null && c.scene != null && !string.IsNullOrEmpty(c.scene.filepath))
            local = c.scene.filepath;
        if (string.IsNullOrEmpty(local)) return null;
        string abs = GFile.Make_Path_Absolute(local);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) return null;
        return abs;
    }

    public static void LoadGlobals()
    {
        if (current == null) return;
        string root = GFile.GetDir_Root(EContentDir.Game);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
        current.RunGlobal(root);
    }
}
