using System.Globalization;
using ImperiumEngine.Assets;
using ImperiumEngine.Script.Node;

namespace ImperiumEngine.Script;

// A compiled A_Script: one ScriptNode per TPulseNode, their pin rows, and the wires between them.
// Built by A_Script.Compile(). Holds no per-run state — that lives on ImpScriptVM — so one program
// can back several running instances.
public class TScriptProgram
{
    public A_Script script;
    public List<ScriptNode> nodes = new();
    public List<TGraphConnection> links = new();
    public List<string> errors = new();

    public ScriptNode Node_Find(Guid id)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].guid == id)
            {
                return nodes[i];
            }
        }
        return null;
    }

    public ScriptNode Event_Find(string member)
    {
        if (string.IsNullOrEmpty(member))
        {
            return null;
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is SN_Event && nodes[i].member == member)
            {
                return nodes[i];
            }
        }
        return null;
    }

    // The one wire feeding an input pin. Inputs take a single wire, so the first hit is it.
    public bool Input_Link(Guid to_node, int to_slot, out Guid from_node, out int from_slot)
    {
        for (int i = 0; i < links.Count; i++)
        {
            TGraphConnection c = links[i];
            if (c.to_node == to_node && c.to_pin == to_slot)
            {
                from_node = c.from_node;
                from_slot = c.from_pin;
                return true;
            }
        }
        from_node = Guid.Empty;
        from_slot = 0;
        return false;
    }

    // First wire leaving an output pin. Exec outputs are followed one way; value outputs fan out
    // and are pulled by the consumer instead.
    public bool Output_Link(Guid from_node, int from_slot, out Guid to_node, out int to_slot)
    {
        for (int i = 0; i < links.Count; i++)
        {
            TGraphConnection c = links[i];
            if (c.from_node == from_node && c.from_pin == from_slot)
            {
                to_node = c.to_node;
                to_slot = c.to_pin;
                return true;
            }
        }
        to_node = Guid.Empty;
        to_slot = 0;
        return false;
    }

    public bool Input_IsWired(Guid to_node, int to_slot)
    {
        return Input_Link(to_node, to_slot, out Guid _, out int _);
    }
}

// A node waiting to resume (SN_Delay). Ticked by ImpScriptVM.Update.
public class TScriptLatent
{
    public ScriptNode node;
    public int out_slot;
    public double remaining;
}

// One running instance of a program, bound to the object it scripts.
// `self` is what an unconnected Target pin means — for a scene script that is the root comp.
public class ImpScriptVM
{
    public const int EXEC_STOP = -1;
    public const int EXEC_LATENT = -2;

    // A runaway loop would hang the editor, so a chain gives up rather than spinning.
    const int STEP_LIMIT = 4096;

    public TScriptProgram program;
    public object self;

    readonly Dictionary<string, object> _locals = new(StringComparer.Ordinal);
    readonly Dictionary<Guid, object[]> _event_args = new();
    readonly List<TScriptLatent> _latent = new();

    public ImpScriptVM(TScriptProgram program, object self)
    {
        this.program = program;
        this.self = self;
    }

    // ------------------------------------------
    // Run
    // ------------------------------------------

    public void Event_Run(string member, object[] args)
    {
        if (program == null)
        {
            return;
        }
        ScriptNode ev = program.Event_Find(member);
        if (ev == null)
        {
            return;
        }
        _event_args[ev.guid] = args;
        Exec_Follow(ev, 0);
    }

    public object[] Event_Args(ScriptNode node)
    {
        if (node == null)
        {
            return null;
        }
        _event_args.TryGetValue(node.guid, out object[] args);
        return args;
    }

    // Walk the exec chain starting at an output pin.
    public void Exec_Follow(ScriptNode node, int out_slot)
    {
        if (program == null || node == null)
        {
            return;
        }
        ScriptNode cur = node;
        int slot = out_slot;
        int steps = 0;
        while (true)
        {
            steps += 1;
            if (steps > STEP_LIMIT)
            {
                Console.WriteLine("[Pulse] exec step limit hit — the graph probably loops.");
                return;
            }
            if (!program.Output_Link(cur.guid, slot, out Guid to_id, out int to_slot))
            {
                return;
            }
            ScriptNode next = program.Node_Find(to_id);
            if (next == null)
            {
                return;
            }
            int cont = next.Exec_Run(this);
            if (cont == EXEC_STOP || cont == EXEC_LATENT)
            {
                return;
            }
            cur = next;
            slot = cont;
        }
    }

    public void Update(double dt)
    {
        for (int i = _latent.Count - 1; i >= 0; i--)
        {
            if (i >= _latent.Count)
            {
                continue;
            }
            TScriptLatent l = _latent[i];
            l.remaining -= dt;
            if (l.remaining > 0.0)
            {
                continue;
            }
            _latent.RemoveAt(i);
            Exec_Follow(l.node, l.out_slot);
        }
    }

    public void Latent_Schedule(ScriptNode node, int out_slot, double seconds)
    {
        _latent.Add(new TScriptLatent
        {
            node = node,
            out_slot = out_slot,
            remaining = seconds,
        });
    }

    // ------------------------------------------
    // Pins
    // ------------------------------------------

    // Value on an input pin: pull it through the wire, or fall back to the pin's saved default.
    public object Input_Get(ScriptNode node, int slot)
    {
        if (node == null)
        {
            return null;
        }
        if (program != null && program.Input_Link(node.guid, slot, out Guid from_id, out int from_slot))
        {
            ScriptNode from = program.Node_Find(from_id);
            if (from != null)
            {
                return from.Value_Get(this, from_slot);
            }
        }
        if (slot < 0 || slot >= node.slots.Count)
        {
            return null;
        }
        TScriptSlot row = node.slots[slot];
        if (node.data != null)
        {
            return node.data.Pin_Get(row.name_left, row.type_left);
        }
        return Pulse.ValueDefault(row.type_left);
    }

    // An unwired Target means Self.
    public object Target_Get(ScriptNode node, int slot)
    {
        if (node != null && program != null && program.Input_IsWired(node.guid, slot))
        {
            object wired = Input_Get(node, slot);
            if (wired != null)
            {
                return wired;
            }
        }
        return self;
    }

    public object Local_Get(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        _locals.TryGetValue(name, out object v);
        return v;
    }

    public void Local_Set(string name, object value)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        _locals[name] = value;
    }

    // Pins are typed but wires only have to be assignable, and saved defaults round-trip
    // through text, so values get coerced on the way into a call.
    public static object Value_As(object v, Type t)
    {
        if (t == null) { return v; }
        if (v == null) { return Pulse.ValueDefault(t); }
        if (t.IsInstanceOfType(v)) { return v; }
        if (t.IsEnum)
        { 
            try { return Enum.ToObject(t, v); }
            catch { return Pulse.ValueDefault(t); }
        } try { return Convert.ChangeType(v, t, CultureInfo.InvariantCulture); }
        catch { return Pulse.ValueDefault(t); }
    }
}
