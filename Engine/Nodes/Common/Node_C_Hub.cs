using Raylib_cs;

namespace ImperiumEngine.Nodes.Common;

[Title("Hub")]
public class Node_C_Hub : ImpFlowNode
{
    [ImpVar] public string hub;

    public override void OnNode_Define()
    {
        universal_node = true;
        inputs.Clear();
    }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        TriggerOutput(0);
    }

    public override Color GetNode_Color() { return Color.Black; }
}

[Title("To Hub")]
public class Node_C_ToHub : ImpFlowNode
{
    [ImpVar] public string hub;

    public override void OnNode_Define()
    {
        universal_node = true;
        outputs.Clear();
    }

    public override void OnNode_Enter(byte pin, ImpFlowNode from)
    {
        ImpFlowNode target = null;
        if (_owner != null)
        {
            string want = hub;
            if (want == null)
            {
                want = "";
            }
            List<ImpFlowNode> hubs = _owner.GetNodes_OfType(typeof(Node_C_Hub));
            for (int i = 0; i < hubs.Count; i++)
            {
                Node_C_Hub h = hubs[i] as Node_C_Hub;
                if (h == null)
                {
                    continue;
                }
                string have = h.hub;
                if (have == null)
                {
                    have = "";
                }
                if (have == want)
                {
                    target = h;
                    break;
                }
            }
        }

        TriggerOutput(0);
        if (target != null && _player != null)
        {
            _player.Node_Enter(target, this, 0);
        }
    }

    public override Color GetNode_Color() { return Color.Black; }
}
