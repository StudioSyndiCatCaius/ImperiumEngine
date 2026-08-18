using ImperiumEngine.Comps._1D;

namespace ImperiumEngine.Nodes.Common;

public class Node_C_Hub : ImpFlowNode
{
    public override void OnNode_Define()
    {
        inputs.Clear();
    }
}

public class Node_C_ToHub : ImpFlowNode
{
    public override void OnNode_Define()
    {
        outputs.Clear();
    }
}