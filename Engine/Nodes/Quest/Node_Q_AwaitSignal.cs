using ImperiumEngine.Assets;
using ImperiumEngine.Assets.Flow;

namespace ImperiumEngine.Nodes.Quest;

[Title("Await Signal")]
public class Node_Q_AwaitSignal : ImpFlowNode
{
    [ImpVar] public string signal;

    public override bool Node_CanUseInFlow(A_Flow flow)
    {
        if (flow is Flow_Quest)
        {
            return true;
        }
        return false;
    }
}
