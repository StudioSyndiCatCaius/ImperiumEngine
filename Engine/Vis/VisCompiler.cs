using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Engine.Assets;
using Engine.Sandbox;
using Engine.Structs;
using Engine.Vis.Nodes;

namespace Engine.Vis;

public static class VisCompiler
{
    public static VisProgram Compile(A_Script script)
    {
        VisProgram program = new();
        if (script == null) return program;

        List<VisInstr> code = new();
        List<object> consts = new();
        List<VisCallSite> calls = new();

        int AddConst(object v)
        {
            consts.Add(v);
            return consts.Count - 1;
        }

        foreach (VisNode n in script.nodes.Values)
        {
            if (n is not VN_Hook_Enter enter) continue;
            if (string.IsNullOrEmpty(enter.hook_name)) continue;
            program.entries[enter.hook_name] = code.Count;
            CompileChain(script, FollowExec(script, enter, 0), code, AddConst, calls);
            code.Add(new VisInstr { op = VisOp.Return });
        }

        program.code = code.ToArray();
        program.constants = consts.ToArray();
        program.calls = calls.ToArray();
        return program;
    }

    static void CompileChain(A_Script script, VisNode? n, List<VisInstr> code,
        Func<object, int> add_const, List<VisCallSite> calls)
    {
        HashSet<ulong> seen = new();
        while (n != null)
        {
            if (!seen.Add(n.id.value)) break;

            if (n is VN_Delay delay)
            {
                int r = 0;
                code.Add(new VisInstr { op = VisOp.LoadConst, a = r, b = add_const(delay.duration) });
                code.Add(new VisInstr { op = VisOp.Delay, a = r });
            }
            else if (n is VN_Func fn)
            {
                MethodInfo? m = fn.Resolve();
                if (m != null)
                {
                    ParameterInfo[] ps = m.GetParameters();
                    for (int i = 0; i < ps.Length; i++)
                    {
                        object? lit = LiteralOf(fn, ps[i]);
                        if (lit != null && !ps[i].ParameterType.IsInstanceOfType(lit))
                        {
                            try { lit = Convert.ChangeType(lit, ps[i].ParameterType, CultureInfo.InvariantCulture); }
                            catch { }
                        }
                        code.Add(new VisInstr { op = VisOp.LoadConst, a = i, b = add_const(lit ?? "") });
                    }
                    VisCallSite? site = BindCall(m);
                    if (site != null)
                    {
                        int idx = calls.Count;
                        calls.Add(site);
                        code.Add(new VisInstr { op = VisOp.Call, a = idx, b = 0, c = ps.Length });
                    }
                }
            }

            n = FollowExec(script, n, 0);
        }
    }

    static object? LiteralOf(VN_Func fn, ParameterInfo p)
    {
        string name = p.Name ?? "";
        if (fn.literals.TryGetValue(name, out object? v)) return v;
        if (p.HasDefaultValue) return p.DefaultValue;
        if (p.ParameterType.IsValueType) return Activator.CreateInstance(p.ParameterType);
        return null;
    }

    static VisNode? FollowExec(A_Script script, VisNode n, int out_pin)
    {
        for (int i = 0; i < script.wires.Count; i++)
        {
            VisWire w = script.wires[i];
            if (w.from != n.id || w.from_pin != out_pin) continue;
            return script.nodes.TryGetValue(w.to, out VisNode? dest) ? dest : null;
        }
        return null;
    }

    static VisCallSite? BindCall(MethodInfo m)
    {
        try
        {
            ParameterExpression target_p = Expression.Parameter(typeof(object), "target");
            ParameterExpression args_p = Expression.Parameter(typeof(object[]), "args");
            ParameterInfo[] ps = m.GetParameters();
            Expression[] call_args = new Expression[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Expression raw = Expression.ArrayIndex(args_p, Expression.Constant(i));
                Type pt = ps[i].ParameterType;
                Expression fallback = Expression.Default(pt);
                Expression converted = Expression.Condition(
                    Expression.Equal(raw, Expression.Constant(null, typeof(object))),
                    fallback,
                    Expression.Convert(raw, pt));
                call_args[i] = converted;
            }
            Expression? inst = m.IsStatic ? null : Expression.Convert(target_p, m.DeclaringType!);
            Expression call = Expression.Call(inst, m, call_args);
            Expression body = m.ReturnType == typeof(void)
                ? call
                : Expression.Block(typeof(void), call);
            Action<object?, object?[]> invoke = Expression.Lambda<Action<object?, object?[]>>(body, target_p, args_p).Compile();
            return new VisCallSite { invoke = invoke, is_static = m.IsStatic, argc = ps.Length };
        }
        catch
        {
            return null;
        }
    }
}
