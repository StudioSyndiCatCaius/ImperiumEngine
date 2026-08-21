using Raylib_cs;

namespace ImperiumEngine.Nodes.Common;

[Title("Start")]
public class Node_C_Start : ImpFlowNode
{
    public override void OnNode_Define()
    {
        universal_node = true;
        inputs.Clear();
    }

    public override Color GetNode_Color() { return Color.Black; }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        TriggerOutput(0);
    }
}
