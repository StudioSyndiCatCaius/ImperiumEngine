using ImperiumEngine.Assets;
using ImperiumEngine.Assets.Flow;

namespace ImperiumEngine.Nodes.Quest;

[Title("Dialogue")]
public class Node_Q_Dialogue : ImpFlowNode
{
    [ImpVar] public Flow_Dialogue dialogue;
    [ImpVar] public Imp3D spawn_point_after; //reference point to spawn the player after the dialogue. NEEDS ti be TRef, and TRef needs to support ImpComp references from other scenes

    public override bool Node_CanUseInFlow(A_Flow flow)
    {
        if (flow is Flow_Quest)
        {
            return true;
        }
        return false;
    }
}
