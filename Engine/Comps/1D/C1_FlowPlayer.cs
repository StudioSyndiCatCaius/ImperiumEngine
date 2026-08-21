using ImperiumEngine.Assets;
using ImperiumEngine.Nodes.Common;

namespace ImperiumEngine.Comps._1D;

public class C1_FlowPlayer : ImpComp
{
    const int STEP_LIMIT = 4096;

    [ImpVar] public A_Flow flow = null;

    private A_Flow _flow_instance; //clones the whole flow asset to modify it without worry. uses guid to match nodes

    private List<ImpFlowNode> nodes_active = new();
    private List<ImpFlowNode> nodes_recorded = new();

    bool _playing;
    int _enter_depth;
    int _steps;

    public override void OnBegin()
    {
        base.OnBegin();
        if (flow != null)
        {
            Start();
        }
    }

    public override void OnEnd()
    {
        Stop();
        base.OnEnd();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!_playing)
        {
            return;
        }

        List<ImpFlowNode> snap = new List<ImpFlowNode>(nodes_active);
        for (int i = 0; i < snap.Count; i++)
        {
            if (!_playing)
            {
                return;
            }
            ImpFlowNode n = snap[i];
            if (n == null)
            {
                continue;
            }
            if (!nodes_active.Contains(n))
            {
                continue;
            }
            n.OnNode_Update((float)dt);
        }

        TryEnd();
    }

    public void Start()
    {
        if (_playing)
        {
            Stop();
        }
        if (flow == null)
        {
            return;
        }

        _flow_instance = flow.Clone() as A_Flow;
        if (_flow_instance == null)
        {
            return;
        }

        nodes_active.Clear();
        nodes_recorded.Clear();
        _steps = 0;
        _enter_depth = 0;
        _playing = true;

        if (on_flow_begin != null)
        {
            on_flow_begin(this, _flow_instance);
        }

        ImpFlowNode start_node = null;
        List<ImpFlowNode> starts = _flow_instance.GetNodes_OfType(typeof(Node_C_Start));
        if (starts.Count > 0)
        {
            start_node = starts[0];
        }
        if (start_node == null)
        {
            Stop();
            return;
        }

        Node_Enter(start_node, null, 0);
        TryEnd();
    }

    public void Stop()
    {
        if (!_playing)
        {
            return;
        }
        _playing = false;

        List<ImpFlowNode> snap = new List<ImpFlowNode>(nodes_active);
        nodes_active.Clear();
        for (int i = 0; i < snap.Count; i++)
        {
            ImpFlowNode n = snap[i];
            if (n == null)
            {
                continue;
            }
            n.on_exit -= Node_Exit;
            n.OnNode_Exit(0);
            if (on_flow_node_end != null)
            {
                on_flow_node_end(this, _flow_instance, n);
            }
        }

        nodes_recorded.Clear();
        _enter_depth = 0;

        if (on_flow_end != null)
        {
            on_flow_end(this, _flow_instance);
        }
    }

    public void Node_Enter(ImpFlowNode node, ImpFlowNode from, byte pin)
    {
        if (!_playing)
        {
            return;
        }
        if (node == null)
        {
            return;
        }
        if (nodes_active.Contains(node))
        {
            return;
        }

        _steps++;
        if (_steps > STEP_LIMIT)
        {
            Stop();
            return;
        }

        _enter_depth++;

        nodes_active.Add(node);
        if (!nodes_recorded.Contains(node))
        {
            nodes_recorded.Add(node);
        }
        node._player = this;
        node._owner = _flow_instance;
        node.on_exit += Node_Exit;

        if (on_flow_node_begin != null)
        {
            on_flow_node_begin(this, _flow_instance, node);
        }

        node.OnNode_Enter(pin, from);

        _enter_depth--;
    }

    private void Node_Exit(ImpFlowNode node, int pin, int con)
    {
        if (node == null)
        {
            return;
        }
        nodes_active.Remove(node);
        node.on_exit -= Node_Exit;
        if (on_flow_node_end != null)
        {
            on_flow_node_end(this, _flow_instance, node);
        }
    }

    void TryEnd()
    {
        if (!_playing)
        {
            return;
        }
        if (_enter_depth != 0)
        {
            return;
        }
        if (nodes_active.Count == 0)
        {
            Stop();
        }
    }

    public bool IsPlaying() { return _playing; }
    public A_Flow GetFlow_Instance() { return _flow_instance; }
    public List<ImpFlowNode> GetNodes_Active() { return nodes_active; }
    public List<ImpFlowNode> GetNodes_Recorded() { return nodes_recorded; }

    public Action<C1_FlowPlayer, A_Flow> on_flow_begin;
    public Action<C1_FlowPlayer, A_Flow> on_flow_end;
    public Action<C1_FlowPlayer, A_Flow, ImpFlowNode> on_flow_node_begin;
    public Action<C1_FlowPlayer, A_Flow, ImpFlowNode> on_flow_node_end;
}
