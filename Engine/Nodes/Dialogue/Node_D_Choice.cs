using ImperiumEngine.Comps._1D;
using ImperiumEngine.Comps._1D.States;
using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Nodes.Dialogue;

public class Node_D_Choice : C2_FlowNode
{
    [ImpVar] public TText text;
}

public class Node_D_ChoiceHUB : C2_FlowNode
{
    public override void OnNode_Enter(int pin, C2_GraphNode previous)
    {
        base.OnNode_Enter(pin, previous);
        sys_Choice.Run(0, (i) =>
        {
            TriggerOutput(pin);
        });
    }
}