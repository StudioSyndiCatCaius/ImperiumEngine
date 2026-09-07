using System.Numerics;
using System.Reflection;
using Engine.Core;
using Engine.Globals;
using Engine.Sandbox;
using Engine.Structs;
using Engine.Vis;
using Engine.Vis.Nodes;

namespace Engine.Assets;

public class A_Script : ImpAsset
{
    public Type class_type = typeof(ImpComp);
    public Dictionary<TGuid64, VisVar> vars = new();
    public Dictionary<TGuid64, VisFunc> funcs = new();
    public Dictionary<TGuid64, VisSignal> signals = new();
    public Dictionary<TGuid64, VisNode> nodes = new();
    public List<VisWire> wires = new();

    VisProgram? _program;

    public VisProgram GetProgram()
    {
        return _program ??= VisCompiler.Compile(this);
    }

    public void Invalidate() { _program = null; }

    public static A_Script MakeTest()
    {
        A_Script s = new() { is_inlined = true, class_type = typeof(ImpComp) };

        VN_Hook_Enter enter = new() { hook_name = "OnBegin", ed_pos = new Vector2(60, 80) };
        enter.id = TGuid64.New();

        VN_Delay delay = new() { duration = 1f, ed_pos = new Vector2(280, 80) };
        delay.id = TGuid64.New();

        VN_Func info = new() { ed_pos = new Vector2(520, 80) };
        info.id = TGuid64.New();
        info.Setup(typeof(GLog).GetMethod(nameof(GLog.Info), BindingFlags.Public | BindingFlags.Static));
        info.literals["msg"] = "Vis: hello";
        info.literals["on_screen"] = false;

        s.nodes[enter.id] = enter;
        s.nodes[delay.id] = delay;
        s.nodes[info.id] = info;
        s.wires.Add(new VisWire { from = enter.id, from_pin = 0, to = delay.id, to_pin = 0 });
        s.wires.Add(new VisWire { from = delay.id, from_pin = 0, to = info.id, to_pin = 0 });
        return s;
    }

    protected override string File_GetExtension() { return "ImpScript"; }

    public override TTable To_Table()
    {
        TTable vars_tbl = new();
        vars_tbl.Set("class_type", class_type?.Name ?? "ImpComp");

        List<object> node_list = new();
        foreach (VisNode n in nodes.Values)
        {
            TTable nt = new();
            nt.Set("type", n.GetType().Name);
            nt.Set("id", n.id.ToString());
            nt.Set("x", n.ed_pos.X);
            nt.Set("y", n.ed_pos.Y);
            if (n.literals.Count > 0)
            {
                TTable lits = new();
                foreach (var pair in n.literals)
                    if (pair.Value != null) lits.Set(pair.Key, pair.Value);
                nt.Set("literals", lits);
            }
            n.Write(nt);
            node_list.Add(nt);
        }
        vars_tbl.Set("nodes", node_list);

        List<object> wire_list = new();
        foreach (VisWire w in wires)
        {
            TTable wt = new();
            wt.Set("from", w.from.ToString());
            wt.Set("to", w.to.ToString());
            wt.Set("from_pin", w.from_pin);
            wt.Set("to_pin", w.to_pin);
            wire_list.Add(wt);
        }
        vars_tbl.Set("wires", wire_list);

        TTable tbl = new();
        tbl.Set("type", "A_Script");
        tbl.Set("vars", vars_tbl);
        return tbl;
    }

    public override void From_Table(TTable tbl)
    {
        TTable v = tbl.get_Table("vars") ?? tbl;
        class_type = TClass<object>.Resolve(v.get_String("class_type")) ?? typeof(ImpComp);
        nodes.Clear();
        wires.Clear();
        _program = null;

        foreach (object o in v.get_List("nodes"))
        {
            if (o is not TTable nt) continue;
            Type? t = TClass<object>.Resolve(nt.get_String("type"));
            if (t == null || Activator.CreateInstance(t) is not VisNode node) continue;
            TGuid64 id = default;
            id.Property_Read(nt.get_String("id"));
            node.id = id;
            node.ed_pos = new Vector2(nt.get_Float("x"), nt.get_Float("y"));
            TTable lits = nt.get_Table("literals");
            if (lits != null)
            {
                foreach (var pair in lits.data)
                    if (pair.Value != null) node.literals[pair.Key.Value] = pair.Value;
            }
            node.Read(nt);
            nodes[node.id] = node;
        }

        foreach (object o in v.get_List("wires"))
        {
            if (o is not TTable wt) continue;
            TGuid64 from = default;
            TGuid64 to = default;
            from.Property_Read(wt.get_String("from"));
            to.Property_Read(wt.get_String("to"));
            wires.Add(new VisWire
            {
                from = from,
                to = to,
                from_pin = wt.get_Int("from_pin"),
                to_pin = wt.get_Int("to_pin"),
            });
        }
    }
}
