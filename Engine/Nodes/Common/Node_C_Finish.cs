using ImperiumEngine.Comps._1D;

namespace ImperiumEngine.Nodes.Common;

public class Node_C_Finish : ImpFlowNode
{
    public override void OnNode_Define()
    {
        outputs.Clear();
    }
}