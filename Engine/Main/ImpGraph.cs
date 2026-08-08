using System.Numerics;
using Raylib_cs;

namespace ImperiumEngine.Main;

// baseclass for graph data
public class ImpGraph
{
    
}

public struct TGraphData
{
    public ImpGraphNode[] nodes;
    public TGraphConnection[] connections;
}

public struct TGraphConnection
{
    public Guid from_node;
    public byte from_pin;
    public Guid to_node;
    public byte to_pin;
}

public struct TGraphNodePin
{
    public string name;
}

public class ImpGraphNode
{
    public Guid guid;
    
    public virtual void OnEnter(byte pin,ValueType val)  { }
    public virtual void OnExit(byte pin, ValueType val) { }
    public virtual void OnUpdate(float dt) { }
    
    public virtual TGraphNodePin[] GetPins_Input() {  return new TGraphNodePin[] { }; }
    public virtual TGraphNodePin[] GetPins_Output() {  return new TGraphNodePin[] { }; }
    
    public virtual Color GetNode_Color() { return Color.White; }
    public virtual String GetNode_Title() { return this.GetType().Name; }
    public virtual Vector2 GetNode_Size() { return Vector2.One; }
    
}