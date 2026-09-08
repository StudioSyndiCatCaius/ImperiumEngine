namespace Engine.Structs;

// A common set up structs for a flow graph, MAINLY used by the editor


public class TGraphData
{
    HashSet<TGraphNode> nodes;
    HashSet<TGraphConnection> connections;
}

public class TGraphConnection
{
    public TGuid32 from_node;
    public TGuid32 to_node;
    public TGuid16 from_pin;
    public TGuid16 to_pin;
}

public class TGraphNode
{
    public TGuid32 id;
}