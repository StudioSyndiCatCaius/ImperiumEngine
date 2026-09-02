using Engine.Comps._1D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Assets;



public class TFlowConnection
{
    public TGuid32 from_node;
    public byte from_pin;
    public TGuid32 to_node;
    public byte to_pin;
}

public class TFlowPin
{
    public string name;
    public TGuid32 guid;
}

public abstract class A_Flow : ImpAsset
{
    [ImpVar] TGuid32 guid;
    [ImpVar] public List<TFlowConnection> connections;
    
    public bool is_instance=false; //if this is a playing instance, instead of a asset file
    public C1_FlowPlayer instance_owner = null;
    
    public List<FlowNode> nodes; // will need to custom read/write
    
    public A_Flow()
    {
        guid=TGuid32.New();
    }
}

public abstract class FlowNode
{
    [ImpVar] public TGuid32 guid;
    [ImpVar] public List<TFlowPin> inputs;
    [ImpVar] public List<TFlowPin> outputs;
    
    public A_Flow flow_owner;

    public virtual void Node_OnEnter(int pin) { }
    public virtual void Node_OnExit(int pin) { }
    public virtual void Node_OnUpdate(double dt) { }

    public void TriggerOutput(int pin, int connection = -1, bool kill_node = true)
    {
        //do node stuff
        if(kill_node) KillNode();
    }

    public void KillNode()
    {
        
    }
    
    public FlowNode()
    {
        guid=TGuid32.New();
    }
}