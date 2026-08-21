using System.Numerics;
using ImperiumEngine.Nodes.Common;

namespace ImperiumEngine.Assets;

public abstract class A_Flow : ImpAsset
{
    [ImpVar(Hidden = true)] public TFlowData Flow = new();
    [ImpVar(Hidden = true)] public Guid guid;

    public A_Flow()
    {
        guid = Guid.NewGuid();
        Flow = new TFlowData();
        Node_C_Start start = new Node_C_Start();
        start.position = new Vector2(80f, 80f);
        start._owner = this;
        Flow.nodes.Add(start);
    }

    public override string File_GetExtension()
    {
        return "ImpFlow";
    }

    public ImpFlowNode Node_Find(Guid id)
    {
        if (Flow.nodes == null)
        {
            return null;
        }
        for (int i = 0; i < Flow.nodes.Count; i++)
        {
            ImpFlowNode n = Flow.nodes[i];
            if (n != null && n.guid == id)
            {
                return n;
            }
        }
        return null;
    }

    public List<ImpFlowNode> GetNodes_Connected(ImpFlowNode node, bool inputs, bool outputs)
    {
        List<ImpFlowNode> result = new();
        if (node == null || Flow.connections == null)
        {
            return result;
        }
        Guid id = node.guid;
        for (int i = 0; i < Flow.connections.Count; i++)
        {
            TFlowConnection c = Flow.connections[i];
            ImpFlowNode found = null;
            if (outputs && c.from_node == id)
            {
                found = Node_Find(c.to_node);
            }
            else if (inputs && c.to_node == id)
            {
                found = Node_Find(c.from_node);
            }
            if (found == null)
            {
                continue;
            }
            if (!result.Contains(found))
            {
                result.Add(found);
            }
        }
        return result;
    }

    public List<ImpFlowNode> GetNodes_OfType(Type type)
    {
        List<ImpFlowNode> result = new();
        if (type == null || Flow.nodes == null)
        {
            return result;
        }
        for (int i = 0; i < Flow.nodes.Count; i++)
        {
            ImpFlowNode n = Flow.nodes[i];
            if (n == null)
            {
                continue;
            }
            if (type.IsAssignableFrom(n.GetType()))
            {
                result.Add(n);
            }
        }
        return result;
    }

    public override ImpAsset Clone()
    {
        A_Flow copy = base.Clone() as A_Flow;
        if (copy == null)
        {
            return null;
        }

        TFlowData data = new TFlowData();
        if (Flow.nodes != null)
        {
            for (int i = 0; i < Flow.nodes.Count; i++)
            {
                ImpFlowNode src = Flow.nodes[i];
                if (src == null)
                {
                    continue;
                }
                ImpFlowNode n = src.Clone();
                if (n == null)
                {
                    continue;
                }
                n._owner = copy;
                data.nodes.Add(n);
            }
        }
        if (Flow.connections != null)
        {
            for (int i = 0; i < Flow.connections.Count; i++)
            {
                data.connections.Add(Flow.connections[i]);
            }
        }
        copy.Flow = data;
        return copy;
    }
}
