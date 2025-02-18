using System.Drawing;
using ImperiumEngine.Source.Objects._2D;
using Silk.NET.Input;

namespace ImperiumEngine.Plugins.PulseVS.Source;

public class PN_If : O2D_PulseNode
{
    public bool Condition = false;
    
    PN_If()
    {
        node_title = "If";
        node_color=Color.Red;
        
        inputs.Add(new FFlowPin("in"));
        
        outputs.Add(new FFlowPin("true"));
        outputs.Add(new FFlowPin("false"));
    }

    public override void Native_Enter(int pin_index)
    {
        if (Condition) { TriggerOutput("true"); }
        else { TriggerOutput("false"); }
    }
}