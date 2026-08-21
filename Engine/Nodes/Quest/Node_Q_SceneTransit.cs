using ImperiumEngine.Assets;
using ImperiumEngine.Assets.Flow;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Nodes.Quest;

[Title("Scene Transit")]
public class Node_Q_SceneTransit : ImpFlowNode
{
    [ImpVar] public TRef<ImpScene> scene;

    public override bool Node_CanUseInFlow(A_Flow flow)
    {
        if (flow is Flow_Quest)
        {
            return true;
        }
        return false;
    }
}
