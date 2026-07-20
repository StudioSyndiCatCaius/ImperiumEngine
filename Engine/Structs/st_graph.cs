namespace ImperiumEngine.Structs;

public struct TGraphData
{
    public TGraphConnection[] connections;
    public TGraphNodeData[] nodes;
}

public struct TGraphNodeData
{
    public Guid id;
    public TGraphPin[] inputs;
    public TGraphPin[] outputs;
}

public struct TGraphConnection
{
    public Guid from_node;
    public Guid from_pin;
    public Guid to_node;
    public Guid to_pin;
}

public enum EGraphPinType
{
    Execute,
    Parameter,
}

public struct TGraphPin
{
    public Guid id;
    public EGraphPinType type;
}