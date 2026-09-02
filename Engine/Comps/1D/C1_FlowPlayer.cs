using System.Numerics;
using Engine.Assets;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;


public class C1_FlowPlayer : Imp1D
{
    [ImpVar] public A_Flow flow = null;

    private A_Flow _flow_instance; //clones the whole flow asset to modify it without worry. uses guid to match nodes

    private List<FlowNode> nodes_active = new();
    private List<FlowNode> nodes_recorded = new();
    
    private bool _playing = false;

    public bool IsPlaying() { return _playing; }
    public A_Flow GetFlow_Instance() { return _flow_instance; }
    public List<FlowNode> GetNodes_Active() { return nodes_active; }
    public List<FlowNode> GetNodes_Recorded() { return nodes_recorded; }

    public Action<C1_FlowPlayer, A_Flow> on_flow_begin;
    public Action<C1_FlowPlayer, A_Flow> on_flow_end;
    public Action<C1_FlowPlayer, A_Flow, FlowNode> on_flow_node_begin;
    public Action<C1_FlowPlayer, A_Flow, FlowNode> on_flow_node_end;
}
