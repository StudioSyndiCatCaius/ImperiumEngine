using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Comps._1D;

public class C1_FlowPlayer : ImpComp
{
    [ImpVar] public A_Flow flow = null;

    private A_Flow _flow_instance = null;
    
    public Action<C1_FlowPlayer,A_Flow> on_flow_begin;
    public Action<C1_FlowPlayer,A_Flow> on_flow_end;
    public Action<C1_FlowPlayer,A_Flow,C2_FlowNode> on_flow_node_begin;
    public Action<C1_FlowPlayer,A_Flow,C2_FlowNode> on_flow_node_end;
}

[ImpClass(Hidden = true)]
public abstract class C2_FlowNode : C2_GraphNode
{
    
}
