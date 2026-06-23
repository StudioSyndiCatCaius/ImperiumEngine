using ImperiumCore.Classes;

namespace ImperiumEngine.Objects._2D;

public struct FGraphEdit_Connection
{
    public Guid from_node;
    public int from_port;
    public Guid to_node;
    public int to_port;
}

public class O2D_GraphEdit : ImpComponent2D
{
    public O2D_ScrollContainer ui_scrollContainer;
    
    public List<FGraphEdit_Connection> connections;
    public List<O2D_GraphNode> nodes;
}