using ImperiumEngine;
using ImperiumEngine;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Assets;

// PULSE is Imperium Engine's visual scripting language.
[AssetColor(110, 200, 200)]
public class A_Pulse : ImpAsset
{
    [ImpVar]  public TClass<ImpComp> parent_type; // parent type to inherit from.
    
    [ImpVar] TGraphData script_graph;
    [ImpVar] private Dictionary<string, ValueType> vars;

}

public abstract class PulseNode : ImpGraphNode
{
    public object owner;
}


public abstract class PulseNode_Func : ImpGraphNode
{
    bool is_execute; // forced true for any function with a void return type. TRUE = Exec function (called when entered). FALSE= Pure Function (called when referenced)
}

public abstract class PulseNode_VarSet : ImpGraphNode
{
    
}

public abstract class PulseNode_VarGet : ImpGraphNode
{
    private bool is_validate; // for bools & objects. spands as Exec with a true/false output pins

}