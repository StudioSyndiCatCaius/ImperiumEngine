using System.Numerics;
using System.Reflection;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Sandbox;

public class Sandbox_Vis : ImpSandbox
{
    public override void Init() { }
    public override void Shutdown() { }
    public override void RunGlobal(string path) { }
    public override TScriptValue? RunInstance(string path, ImpComp owner) { return null; }

    public override TScriptValue? RunInstance(A_Script script, ImpComp owner)
    {
        if (script == null || owner == null) return null;
        VisProgram program = script.GetProgram();
        if (program.entries.Count == 0) return null;
        VisInstance v = new() { program = program, owner = owner };
        return new TScriptValue { sandbox = this, owner = owner, handle = v };
    }

    public override bool Has(TScriptValue inst, string name)
    {
        return inst.handle is VisInstance v && v.program.entries.ContainsKey(name);
    }

    public override void Call(TScriptValue inst, string name)
    {
        if (inst.handle is not VisInstance v) return;
        if (!v.program.entries.TryGetValue(name, out int ip)) return;
        Array.Clear(v.regs);
        v.Run(ip, v.regs);
    }

    public override void Tick(TScriptValue inst, double dt)
    {
        if (inst.handle is not VisInstance v) return;
        for (int i = v.latents.Count - 1; i >= 0; i--)
        {
            VisLatent lat = v.latents[i];
            lat.remaining -= (float)dt;
            if (lat.remaining > 0) continue;
            v.latents.RemoveAt(i);
            v.Run(lat.resume_ip, lat.regs);
        }
    }
}

public struct VisVar
{
    public TLabel name;
}

public struct VisFunc
{
    public TLabel name;
}

public struct VisSignal
{
    public TLabel name;
}

public struct VisExec { }

public struct VisPin
{
    public TLabel name;
    public Type type;
    public bool IsExec => type == typeof(VisExec);
}

public struct VisWire
{
    public TGuid64 from;
    public TGuid64 to;
    public int from_pin;
    public int to_pin;
}

public class VisNode
{
    public TGuid64 id;
    public Vector2 ed_pos;
    public VisPin[] inputs = Array.Empty<VisPin>();
    public VisPin[] outputs = Array.Empty<VisPin>();
    public Dictionary<string, object> literals = new();

    public virtual Color ED_GetTitleColor() { return Color.White; }
    public virtual string ED_GetTitle()
    {
        return GetType().GetCustomAttribute<TitleAttribute>()?.Name ?? GetType().Name;
    }

    public virtual Vector2 ED_Size()
    {
        int inn = 0, outn = 0;
        for (int i = 0; i < inputs.Length; i++)
            if (!inputs[i].IsExec) inn++;
        for (int i = 0; i < outputs.Length; i++)
            if (!outputs[i].IsExec) outn++;
        int rows = Math.Max(inn, outn);
        return new Vector2(200, 26 + Math.Max(1, rows) * 20 + 6);
    }

    public virtual void Write(TTable tbl) { }
    public virtual void Read(TTable tbl) { }
}

public enum VisOp : byte { Nop, LoadConst, Call, Delay, Return }

public struct VisInstr
{
    public VisOp op;
    public int a, b, c;
}

public class VisCallSite
{
    public Action<object?, object?[]>? invoke;
    public bool is_static;
    public int argc;
}

public class VisProgram
{
    public VisInstr[] code = Array.Empty<VisInstr>();
    public object[] constants = Array.Empty<object>();
    public VisCallSite[] calls = Array.Empty<VisCallSite>();
    public Dictionary<string, int> entries = new();
}

public class VisLatent
{
    public float remaining;
    public int resume_ip;
    public object?[] regs = Array.Empty<object?>();
}

public class VisInstance
{
    public VisProgram program = new();
    public ImpComp? owner;
    public object?[] regs = new object?[16];
    public object?[] argbuf = new object?[8];
    public List<VisLatent> latents = new();

    public void Run(int start_ip, object?[] r)
    {
        VisInstr[] code = program.code;
        int n = code.Length;
        int ip = start_ip;
        int guard = 0;
        while ((uint)ip < (uint)n && guard++ < 10000)
        {
            VisInstr i = code[ip++];
            switch (i.op)
            {
                case VisOp.LoadConst:
                    if ((uint)i.a < (uint)r.Length && (uint)i.b < (uint)program.constants.Length)
                        r[i.a] = program.constants[i.b];
                    break;
                case VisOp.Call:
                {
                    if ((uint)i.a >= (uint)program.calls.Length) break;
                    VisCallSite site = program.calls[i.a];
                    if (argbuf.Length < site.argc)
                        argbuf = new object?[Math.Max(site.argc, 8)];
                    for (int k = 0; k < site.argc; k++)
                        argbuf[k] = (uint)(i.b + k) < (uint)r.Length ? r[i.b + k] : null;
                    try { site.invoke?.Invoke(site.is_static ? null : owner, argbuf); }
                    catch (Exception e) { GLog.Error("Vis: " + e.Message); }
                    break;
                }
                case VisOp.Delay:
                {
                    float dur = 0f;
                    if ((uint)i.a < (uint)r.Length && r[i.a] != null)
                        dur = Convert.ToSingle(r[i.a]);
                    object?[] saved = new object?[r.Length];
                    Array.Copy(r, saved, r.Length);
                    latents.Add(new VisLatent { remaining = dur, resume_ip = ip, regs = saved });
                    return;
                }
                case VisOp.Return:
                    return;
            }
        }
    }
}
