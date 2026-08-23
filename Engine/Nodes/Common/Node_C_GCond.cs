using ImperiumEngine.Assets;
using Raylib_cs;

namespace ImperiumEngine.Nodes.Common;

public class Node_C_GCond : ImpFlowNode
{
    [ImpVar] public List<A_GlobalCondtion> conditions;
    
    public override string GetNode_Title() { return "Global Condition"; }
    public override Color GetNode_Color() { return Color.Red; }
    public override string GetNode_Category() { return "Scripting"; }

    public override void OnNode_Define()
    {
        outputs =
        [
            new(){ name = "true"},
            new(){ name = "false"},
        ];

    }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        base.OnNode_Enter(pin, from);
        if (A_GlobalCondtion.Check(conditions))
        {
            TriggerOutput(0);
        }
        else
        {
            TriggerOutput(1);
        }
    }
}