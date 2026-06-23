using System.Drawing;
using ImperiumCore.Classes;

namespace ImperiumEngine.Objects._2D;

public struct TGraphNode_Pin
{
    public string name;
    public int type;
}

public class O2D_GraphNode  : ImpComponent2D
{ 
    public Guid guid;

    public List<TGraphNode_Pin> pin_input;
    public List<TGraphNode_Pin> pin_output;

    public string Node_GetTitle()
    {
        return "";
    }
    public Color Node_GetColor()
    {
        return Color.White;
    }
    
}