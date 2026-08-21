using Raylib_cs;

namespace ImperiumEngine.Nodes.Common;

[Title("Finish")]
public class Node_C_Finish : ImpFlowNode
{
    public override void OnNode_Define()
    {
        universal_node = true;
        outputs.Clear();
    }

    public override Color GetNode_Color() { return Color.Black; }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        if (_player != null)
        {
            _player.Stop();
        }
    }
}
