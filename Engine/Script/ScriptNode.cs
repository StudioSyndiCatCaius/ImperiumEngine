using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Script.Node;
using Raylib_cs;

namespace ImperiumEngine.Script;

// One row of a node: a left (input) pin and/or a right (output) pin.
// Mirrors C2_GraphNode's slot layout so the editor can build a widget from it,
// without the generic graph widget knowing anything about Pulse.
// An enabled pin with a null type is an exec pin.
public class TScriptSlot
{
    public bool enable_left;
    public Type type_left;
    public string name_left = "";

    public bool enable_right;
    public Type type_right;
    public string name_right = "";
}

// A node the user can pick out of the graph's node menu.
public class TScriptNodeMenu
{
    public string category = "";
    public string text = "";
    public string node_class = "";
    public EScriptNodeType kind;
    public string member = "";
    public bool is_set;
    public bool is_disabled;
}

public class ScriptNode : ImpGraphNode
{
    public object owner;

    // TRUE  = an instance of this node can be added anywhere from the node menu (SN_If, SN_Delay).
    // FALSE = the node only exists as an instance of something reflected off the target type:
    //         SN_Func per [PulseCall], SN_Event per [PulseOverride], SN_VarGet / SN_VarSet per [ImpVar].
    public bool is_available = false;

    // Type the member was reflected off (TPulseNode.target_type), and the member itself.
    public Type context = typeof(object);
    public string member = "";

    // Script the node was instanced from. Set by ScriptNodes.Create; needed for script-local vars.
    public A_Script script;

    // Compile output. `data` is the serialized node (pin defaults live there); `slots` is the
    // pin layout the VM indexes connections against. Both are filled by A_Script.Compile.
    public TPulseNode data;
    public List<TScriptSlot> slots = new();

    // Pull the serialized node onto this instance. Any [ImpVar] on the node class is filled
    // from the matching pin default, so node fields and pin rows stay the same value.
    public virtual void Bind(TPulseNode node, Type ctx)
    {
        context = ctx;
        if (context == null)
        {
            context = typeof(object);
        }
        if (node == null)
        {
            return;
        }
        guid = node.id;
        data = node;
        member = node.member;
        if (member == null)
        {
            member = "";
        }
        FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo f = fields[i];
            if (f.GetCustomAttribute<ImpVarAttribute>() == null)
            {
                continue;
            }
            object v = node.Pin_Get(f.Name, f.FieldType);
            if (v != null)
            {
                f.SetValue(this, v);
            }
        }
    }

    public override string GetNode_Title()
    {
        return member;
    }

    public override Color GetNode_Color()
    {
        return new Color(70, 120, 180, 255);
    }

    public virtual EGraphNodeChrome GetNode_Chrome()
    {
        return EGraphNodeChrome.Regular;
    }

    // Describe the node's pin rows, top to bottom. The editor turns these into graph slots.
    public virtual void Slots_Build(List<TScriptSlot> slots)
    {
    }

    // ------------------------------------------
    // Runtime
    // ------------------------------------------

    // Resolve / validate against the compiled program. One line per problem; the graph does not
    // run any better for it, but the Compile button reports them.
    public virtual void Compile_Check(TScriptProgram program, List<string> errors)
    {
    }

    // Run this node as one exec step. Returns the exec OUTPUT slot to continue from,
    // or ImpScriptVM.EXEC_STOP to end the chain / ImpScriptVM.EXEC_LATENT if the node resumes itself.
    public virtual int Exec_Run(ImpScriptVM vm)
    {
        return ImpScriptVM.EXEC_STOP;
    }

    // Value this node produces on output pin `slot` (pure calls, var gets, event args).
    public virtual object Value_Get(ImpScriptVM vm, int slot)
    {
        return null;
    }
}

public abstract class ScriptNodeAsync : ScriptNode
{
    public ScriptNodeAsync()
    {
        is_available = true;
    }
}

// Reflection over the SN_* node classes: what can be added, and how to instance a saved node.
public static class ScriptNodes
{
    static Dictionary<string, Type> _by_name;
    static List<ScriptNode> _protos;

    public static ScriptNode Create(TPulseNode pn, Type ctx, A_Script script)
    {
        if (pn == null)
        {
            return null;
        }
        Scan();
        Type t = null;
        if (!string.IsNullOrEmpty(pn.node_class))
        {
            _by_name.TryGetValue(pn.node_class, out t);
        }
        if (t == null)
        {
            // Nodes saved before node_class existed only carry a kind.
            if (pn.kind == EScriptNodeType.VoidOverride)
            {
                t = typeof(SN_Event);
            }
            else if (pn.kind == EScriptNodeType.Var)
            {
                if (pn.is_set)
                {
                    t = typeof(SN_VarSet);
                }
                else
                {
                    t = typeof(SN_VarGet);
                }
            }
            else
            {
                t = typeof(SN_Func);
            }
        }
        ScriptNode node = Activator.CreateInstance(t) as ScriptNode;
        if (node == null)
        {
            return null;
        }
        node.script = script;
        node.Bind(pn, ctx);
        return node;
    }

    // Everything that can be dropped on the graph for this context type.
    // self_ctx is false when the menu was opened by dragging a wire off an object pin:
    // then only that object's own calls and vars make sense.
    public static List<TScriptNodeMenu> Menu(Type ctx, bool self_ctx, A_Script script)
    {
        List<TScriptNodeMenu> list = new();
        if (ctx == null)
        {
            return list;
        }
        Scan();

        if (self_ctx)
        {
            List<TPulseFunc> ovs = Pulse.Overrides(ctx);
            for (int i = 0; i < ovs.Count; i++)
            {
                TPulseFunc f = ovs[i];
                bool has = false;
                if (script != null && script.Node_FindOverride(f.name) != null)
                {
                    has = true;
                }
                list.Add(new TScriptNodeMenu
                {
                    category = "Add Override",
                    text = f.name,
                    node_class = nameof(SN_Event),
                    kind = EScriptNodeType.VoidOverride,
                    member = f.name,
                    is_disabled = has,
                });
            }
        }

        List<TPulseFunc> calls = Pulse.Calls(ctx);
        for (int i = 0; i < calls.Count; i++)
        {
            TPulseFunc f = calls[i];
            list.Add(new TScriptNodeMenu
            {
                category = "Call Function",
                text = f.name,
                node_class = nameof(SN_Func),
                kind = EScriptNodeType.Func,
                member = f.name,
            });
        }

        // Reflected [ImpVar]s, then the script's own vars. Both are gated on EImpVarEdit.
        List<TPulseVar> vars = Pulse.Vars(ctx);
        List<string> names = new();
        List<bool> sets = new();
        for (int i = 0; i < vars.Count; i++)
        {
            names.Add(vars[i].name);
            sets.Add(vars[i].can_set);
        }
        if (script != null && script.vars != null)
        {
            for (int i = 0; i < script.vars.Count; i++)
            {
                TScriptVar sv = script.vars[i];
                if (sv.edit == EImpVarEdit.None)
                {
                    continue;
                }
                names.Add(sv.name);
                sets.Add(sv.edit == EImpVarEdit.ReadWrite);
            }
        }
        for (int i = 0; i < names.Count; i++)
        {
            list.Add(new TScriptNodeMenu
            {
                category = "Variables",
                text = "Get " + names[i],
                node_class = nameof(SN_VarGet),
                kind = EScriptNodeType.Var,
                member = names[i],
            });
            if (sets[i])
            {
                list.Add(new TScriptNodeMenu
                {
                    category = "Variables",
                    text = "Set " + names[i],
                    node_class = nameof(SN_VarSet),
                    kind = EScriptNodeType.Var,
                    member = names[i],
                    is_set = true,
                });
            }
        }

        if (self_ctx)
        {
            List<TScriptNodeMenu> flow = new();
            for (int i = 0; i < _protos.Count; i++)
            {
                ScriptNode proto = _protos[i];
                if (!proto.is_available)
                {
                    continue;
                }
                EScriptNodeType kind = EScriptNodeType.Branch;
                if (proto is ScriptNodeAsync)
                {
                    kind = EScriptNodeType.Async;
                }
                flow.Add(new TScriptNodeMenu
                {
                    category = "Flow",
                    text = proto.GetNode_Title(),
                    node_class = proto.GetType().Name,
                    kind = kind,
                });
            }
            flow.Sort((a, b) => string.Compare(a.text, b.text, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < flow.Count; i++)
            {
                list.Add(flow[i]);
            }
        }

        return list;
    }

    static void Scan()
    {
        if (_by_name != null)
        {
            return;
        }
        _by_name = new Dictionary<string, Type>(StringComparer.Ordinal);
        _protos = new List<ScriptNode>();
        Type[] types = typeof(ScriptNode).Assembly.GetTypes();
        for (int i = 0; i < types.Length; i++)
        {
            Type t = types[i];
            if (t.IsAbstract || !typeof(ScriptNode).IsAssignableFrom(t) || t == typeof(ScriptNode))
            {
                continue;
            }
            _by_name[t.Name] = t;
            ScriptNode proto = null;
            try
            {
                proto = Activator.CreateInstance(t) as ScriptNode;
            }
            catch
            {
                proto = null;
            }
            if (proto != null)
            {
                _protos.Add(proto);
            }
        }
    }
}
