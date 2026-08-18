using System.Numerics;
using ImperiumEngine.Comps._1D;
using Raylib_cs;

namespace ImperiumEngine;



public struct TFlowData
{
    public ImpFlowNode[] nodes;
    public TFlowConnection[] connections;
}

public struct TFlowConnection
{
    public Guid from_node;
    public byte from_pin;
    public Guid to_node;
    public byte to_pin;
}

public struct TFlowNodePin
{
    public string name;
}

public class ImpFlowNode
{
    public A_Flow _owner;
    public bool universal_node; 
    public Guid guid;

    public List<TFlowNodePin> inputs = new() { new() };
    public List<TFlowNodePin> outputs = new() { new() };

    public Action<ImpFlowNode,int,int> on_exit;

    public void TriggerOutput(int pin, int connections = -1)
    {
        //if connection > -1, only trigger the out connected node of that index. if -1, trigger all out connections.
    }
    
    public virtual void OnNode_Define() { }
    public virtual void OnNode_Enter(byte pin,ImpFlowNode from)  { }
    public virtual void OnNode_Exit(byte pin) { }
    public virtual void OnNode_Update(float dt) { }
    
    public virtual bool Node_CanUseInFlow(A_Flow flow) { return true; }
    
    public virtual Color GetNode_Color() { return Color.White; }
    public virtual String GetNode_Title() { return this.GetType().Name; }
    public virtual Vector2 GetNode_Size() { return Vector2.One; }
    
}