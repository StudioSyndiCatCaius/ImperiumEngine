using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Nodes.Common;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public class C1_FlowPlayer : ImpComp
{
    [ImpVar] public A_Flow flow = null;

    private A_Flow _flow_instance; //clones the whole flow asset to modify it without worry. uses guid to match nodes
    
    private List<ImpFlowNode> nodes_active;
    private List<ImpFlowNode> nodes_recorded;

    public void Start()
    {
        ImpFlowNode _start_node = null; //get first node of classe Node_C_Start
        Node_Enter(_start_node,null,0);
    }

    public void Stop()
    {
        nodes_active.Clear();
        nodes_recorded.Clear();
        on_flow_end.Invoke(this,_flow_instance);
    }

    public void Node_Enter(ImpFlowNode node,ImpFlowNode from, byte pin)
    {
        //node.OnNode_Enter(pin,from);
        nodes_active.Add(node);
        node.on_exit += Node_Exit;
    }

    private void Node_Exit(ImpFlowNode node, int pin, int con)
    {
        nodes_active.Remove(node);
        node.on_exit-=Node_Exit;
        
    }
    

    
    public List<ImpFlowNode> GetNodes_Active() { return nodes_active; }
    public List<ImpFlowNode> GetNodes_Recorded() { return nodes_recorded; }
    
    public Action<C1_FlowPlayer,A_Flow> on_flow_begin;
    public Action<C1_FlowPlayer,A_Flow> on_flow_end;
    public Action<C1_FlowPlayer,A_Flow,ImpFlowNode> on_flow_node_begin;
    public Action<C1_FlowPlayer,A_Flow,ImpFlowNode> on_flow_node_end;
}

public class A_Flow : ImpAsset
{
    public TFlowData Flow;
    public Guid guid;

    public List<ImpFlowNode> GetNodes_Connected(ImpFlowNode node, bool inputs, bool outputs)
    {
        return null;
    }
    
    public List<ImpFlowNode> GetNodes_OfType(Type type)
    {
        return null;
    }

}




