using ImperiumEngine.Assets;
using Raylib_cs;

namespace ImperiumEngine.Nodes.Common;

public class Node_C_GScript : ImpFlowNode
{
    [ImpVar] public List<A_GlobalScript> scripts;
    
    public override string GetNode_Title() { return "Global Script"; }
    public override Color GetNode_Color() { return Color.Blue; }
    public override string GetNode_Category() { return "Scripting"; }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        base.OnNode_Enter(pin, from);
        A_GlobalScript.Run(scripts);
        TriggerOutput(0);
    }
}