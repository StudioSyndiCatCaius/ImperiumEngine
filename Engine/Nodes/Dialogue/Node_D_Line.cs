using ImperiumEngine.Assets;
using ImperiumEngine.Assets.Flow;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Nodes.Dialogue;

[Title("Line")]
public class Node_D_Line : ImpFlowNode
{
    [ImpVar] public ImpAsset speaker;
    [ImpVar] public TText text;

    public override bool Node_CanUseInFlow(A_Flow flow)
    {
        if (flow is Flow_Dialogue)
        {
            return true;
        }
        return false;
    }
}
