using System.Numerics;
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
    public Guid guid;

    public List<TFlowNodePin> inputs = new() { new() };
    public List<TFlowNodePin> outputs = new() { new() };

    public Action<ImpFlowNode,int,int> on_exit;

    public void TriggerOutput(int pin, int connections = -1)
    {
        on_exit.Invoke(this,pin,connections);
    }
    
    public virtual void OnNode_Define() { }
    public virtual void OnNode_Enter(byte pin,ValueType val)  { }
    public virtual void OnNode_Exit(byte pin, ValueType val) { }
    public virtual void OnNode_Update(float dt) { }
    
    public virtual Color GetNode_Color() { return Color.White; }
    public virtual String GetNode_Title() { return this.GetType().Name; }
    public virtual Vector2 GetNode_Size() { return Vector2.One; }
    
}