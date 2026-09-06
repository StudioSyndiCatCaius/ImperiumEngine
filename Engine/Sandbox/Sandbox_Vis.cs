using Engine.Core;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Sandbox;

public class Sandbox_Vis : ImpSandbox
{
    public override void Init() { }
    public override void Shutdown() { }
    public override void RunGlobal(string path) { }
    public override TScriptValue? RunInstance(string path, ImpComp owner) { return null; }
    public override bool Has(TScriptValue inst, string name) { return false; }
    public override void Call(TScriptValue inst, string name) { }
}


public struct VisVar
{
    public TLabel name;
}

public struct VisFunc
{
    public TLabel name;
}

public struct VisSignal
{
    public TLabel name;
}

public struct VisExec // type for an execution pin
{
    
}

public struct VisPin
{
    public TLabel name;
    public Type type;
}

public class VisNode
{
    
    // ==========================================================
    // Editor
    // ==========================================================
    public VisPin[] inputs;
    public VisPin[] outputs;
    
    public Color ED_GetTitleColor() { return Color.White; }
}

public class VisGraph : ImpAsset
{
    public Type class_type; //typically this will be ImpComp (and maybe ImpAsset so you can create custom asset types)
    public Dictionary<TGuid64, VisVar> vars;
    public Dictionary<TGuid64, VisFunc> funcs;
    public Dictionary<TGuid64, VisSignal> signals;
    public Dictionary<TGuid64, VisNode> nodes;
    
    public TByteBlock compiled_script;//not sure if this is the best/fastest/most performant way to contain a compiled script?
}