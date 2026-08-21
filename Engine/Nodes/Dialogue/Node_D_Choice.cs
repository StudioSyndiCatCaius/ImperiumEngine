using ImperiumEngine.Assets;
using ImperiumEngine.Assets.Flow;
using ImperiumEngine.Comps._1D.States;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Nodes.Dialogue;

[Title("Choice")]
public class Node_D_Choice : ImpFlowNode
{
    [ImpVar] public TText text;

    public override bool Node_CanUseInFlow(A_Flow flow)
    {
        if (flow is Flow_Dialogue)
        {
            return true;
        }
        return false;
    }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        TriggerOutput(0);
    }

    public override Color GetNode_Color() { return Color.Green; }
}

[Title("Choice Hub")]
public class Node_D_ChoiceHUB : ImpFlowNode
{
    public override bool Node_CanUseInFlow(A_Flow flow)
    {
        if (flow is Flow_Dialogue)
        {
            return true;
        }
        return false;
    }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        List<TText> texts = new();
        List<int> wires = new();
        int match_i = 0;
        if (_owner != null && _owner.Flow.connections != null)
        {
            for (int i = 0; i < _owner.Flow.connections.Count; i++)
            {
                TFlowConnection c = _owner.Flow.connections[i];
                if (c.from_node != guid)
                {
                    continue;
                }
                if ((int)c.from_pin != 0)
                {
                    continue;
                }
                ImpFlowNode n = _owner.Node_Find(c.to_node);
                Node_D_Choice choice = n as Node_D_Choice;
                if (choice != null)
                {
                    texts.Add(choice.text);
                    wires.Add(match_i);
                }
                match_i++;
            }
        }

        void OnPicked(int index)
        {
            if (index < 0 || index >= wires.Count)
            {
                return;
            }
            TriggerOutput(0, wires[index]);
        }

        sys_Choice.Run(texts, OnPicked);
    }
    
    public override Color GetNode_Color() { return Color.Green; }
}
