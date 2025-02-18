using System.Drawing;

namespace ImperiumEngine.Source.Objects._2D;

public class O2D_FlowGraph : ImpComponent2D
{
    
}

public class O2D_FlowNode : ImpComponent2D
{
    public string node_title = "";
    public Color node_color;
    
    public List<FFlowPin> inputs = new();
    public List<FFlowPin> outputs = new();
}

public struct FFlowPin
{
    public string pin_name = "";
    public string display_name = "";

    public FFlowPin(string name)
    {
        pin_name = name;
    }
}

