using System.Collections;
using System.Numerics;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using Raylib_cs;

namespace ImperiumEngine;

public struct TFlowData
{
    public List<ImpFlowNode> nodes = new();
    public List<TFlowConnection> connections = new();

    public TFlowData()
    {
    }
}

public struct TFlowConnection
{
    public Guid from_node;
    public byte from_pin;
    public Guid to_node;
    public byte to_pin;
}

public struct TFlowNodePin
{
    public string name;
}

public abstract class ImpFlowNode
{
    public A_Flow _owner;
    public C1_FlowPlayer _player;
    public bool universal_node;
    public Guid guid;
    public Vector2 position;

    public List<TFlowNodePin> inputs = new() { new() };
    public List<TFlowNodePin> outputs = new() { new() };

    public Action<ImpFlowNode, int, int> on_exit;

    static Dictionary<string, Type> _types;

    public ImpFlowNode()
    {
        guid = Guid.NewGuid();
        OnNode_Define();
    }

    public static Type Type_FromName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        Types_Scan();
        Type t;
        if (_types.TryGetValue(name, out t))
        {
            return t;
        }
        return null;
    }

    static void Types_Scan()
    {
        if (_types != null)
        {
            return;
        }
        _types = new Dictionary<string, Type>(StringComparer.Ordinal);
        Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
        for (int a = 0; a < asms.Length; a++)
        {
            Type[] types;
            try
            {
                types = asms[a].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Type[] loaded = ex.Types;
                if (loaded == null)
                {
                    continue;
                }
                types = loaded;
            }
            if (types == null)
            {
                continue;
            }
            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t == null || t.IsAbstract)
                {
                    continue;
                }
                if (!typeof(ImpFlowNode).IsAssignableFrom(t))
                {
                    continue;
                }
                _types[t.Name] = t;
            }
        }
    }

    // if connection > -1, only trigger the out connected node of that index. if -1, trigger all out connections.
    public void TriggerOutput(int pin, int connections = -1)
    {
        if (_player == null)
        {
            return;
        }
        if (!_player.IsPlaying())
        {
            return;
        }

        List<TFlowConnection> matches = new();
        if (_owner != null && _owner.Flow.connections != null)
        {
            for (int i = 0; i < _owner.Flow.connections.Count; i++)
            {
                TFlowConnection c = _owner.Flow.connections[i];
                if (c.from_node == guid && (int)c.from_pin == pin)
                {
                    matches.Add(c);
                }
            }
        }

        List<TFlowConnection> fire = new();
        if (connections < 0)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                fire.Add(matches[i]);
            }
        }
        else if (connections < matches.Count)
        {
            fire.Add(matches[connections]);
        }

        if (on_exit != null)
        {
            on_exit(this, pin, connections);
        }
        OnNode_Exit((byte)pin);

        if (_player == null)
        {
            return;
        }
        if (!_player.IsPlaying())
        {
            return;
        }
        if (_owner == null)
        {
            return;
        }

        for (int i = 0; i < fire.Count; i++)
        {
            if (!_player.IsPlaying())
            {
                return;
            }
            TFlowConnection c = fire[i];
            ImpFlowNode next = _owner.Node_Find(c.to_node);
            if (next == null)
            {
                continue;
            }
            _player.Node_Enter(next, this, c.to_pin);
        }
    }

    public ImpFlowNode Clone()
    {
        ImpFlowNode copy = Activator.CreateInstance(GetType()) as ImpFlowNode;
        if (copy == null)
        {
            return null;
        }

        FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo f = fields[i];
            if (f.IsInitOnly || f.IsLiteral)
            {
                continue;
            }
            if (f.Name == "_owner" || f.Name == "_player" || f.Name == "on_exit")
            {
                continue;
            }

            object v = f.GetValue(this);
            if (v is IList src && f.FieldType.IsGenericType)
            {
                IList dst = Activator.CreateInstance(f.FieldType) as IList;
                if (dst != null)
                {
                    for (int n = 0; n < src.Count; n++)
                    {
                        dst.Add(src[n]);
                    }
                    f.SetValue(copy, dst);
                }
            }
            else
            {
                f.SetValue(copy, v);
            }
        }

        copy._owner = null;
        copy._player = null;
        copy.on_exit = null;
        return copy;
    }

    public virtual void OnNode_Define() { }
    public virtual void OnNode_Enter(byte pin, ImpFlowNode from) { }
    public virtual void OnNode_Exit(byte pin) { }
    public virtual void OnNode_Update(float dt) { }

    public virtual bool Node_CanUseInFlow(A_Flow flow) { return true; }

    public virtual bool Node_IsEditorAddable() { return true; }

    public virtual string GetNode_Category()
    {
        if (universal_node)
        {
            return "Common";
        }
        string ns = GetType().Namespace ?? "";
        int last = ns.LastIndexOf('.');
        if (last >= 0 && last + 1 < ns.Length)
        {
            return ns.Substring(last + 1);
        }
        return "Flow";
    }

    public virtual Color GetNode_Color()
    {
        string cat = GetNode_Category();
        if (cat == "Common")
        {
            return new Color(90, 90, 104, 255);
        }
        if (cat == "Quest")
        {
            return new Color(180, 120, 60, 255);
        }
        return new Color(70, 120, 180, 255);
    }

    public virtual string GetNode_Title()
    {
        TitleAttribute title = GetType().GetCustomAttribute<TitleAttribute>();
        if (title != null && !string.IsNullOrEmpty(title.Name))
        {
            return title.Name;
        }
        string n = GetType().Name;
        if (n.Length > 7 && n.StartsWith("Node_") && n[6] == '_')
        {
            return n.Substring(7);
        }
        return n;
    }

    public virtual Vector2 GetNode_Size() { return new Vector2(180f, 80f); }

    static List<ImpFlowNode> _protos_addable;

    public static List<ImpFlowNode> Nodes_Addable(A_Flow flow)
    {
        Protos_Scan();
        List<ImpFlowNode> list = new();
        for (int i = 0; i < _protos_addable.Count; i++)
        {
            ImpFlowNode proto = _protos_addable[i];
            if (!proto.Node_CanUseInFlow(flow))
            {
                continue;
            }
            list.Add(proto);
        }
        list.Sort((a, b) =>
        {
            int cat = string.Compare(a.GetNode_Category(), b.GetNode_Category(), StringComparison.OrdinalIgnoreCase);
            if (cat != 0)
            {
                return cat;
            }
            return string.Compare(a.GetNode_Title(), b.GetNode_Title(), StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    static void Protos_Scan()
    {
        if (_protos_addable != null)
        {
            return;
        }
        Types_Scan();
        _protos_addable = new List<ImpFlowNode>();
        foreach (KeyValuePair<string, Type> kv in _types)
        {
            Type t = kv.Value;
            ImpFlowNode n = Activator.CreateInstance(t) as ImpFlowNode;
            if (n == null)
            {
                continue;
            }
            if (!n.Node_IsEditorAddable())
            {
                continue;
            }
            _protos_addable.Add(n);
        }
    }
}
