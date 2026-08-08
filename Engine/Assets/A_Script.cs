using ImperiumEngine.Main;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Assets;

public class A_Script : ImpAsset
{
    [ImpVar][Export] public TClass<ImpComp> parent_type; // parent type to inherit from.
    
    [ImpVar] TGraphData script_graph;
    [ImpVar] private Dictionary<string, ValueType> vars;

}