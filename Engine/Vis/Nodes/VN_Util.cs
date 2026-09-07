using System.Reflection;
using Engine.Sandbox;
using Engine.Structs;

namespace Engine.Vis.Nodes;

public class VN_Var : VisNode //Gets a reference to a variable. requires a "owner" input of the script calling this node does not already
{
    
}

public class VN_Func : VisNode //base node for a callable function (Using "ScriptCall")
{
    public string type_name = "";
    public string method_name = "";

    public VN_Func()
    {
        inputs = new[] { new VisPin { name = "exec", type = typeof(VisExec) } };
        outputs = new[] { new VisPin { name = "then", type = typeof(VisExec) } };
    }

    public void Setup(MethodInfo? m)
    {
        if (m == null) return;
        type_name = m.DeclaringType?.Name ?? "";
        method_name = m.Name;
        ParameterInfo[] ps = m.GetParameters();
        VisPin[] ins = new VisPin[ps.Length + 1];
        ins[0] = new VisPin { name = "exec", type = typeof(VisExec) };
        for (int i = 0; i < ps.Length; i++)
            ins[i + 1] = new VisPin { name = ps[i].Name ?? ("p" + i), type = ps[i].ParameterType };
        inputs = ins;
        outputs = new[] { new VisPin { name = "then", type = typeof(VisExec) } };
    }

    public MethodInfo? Resolve()
    {
        Type? t = TClass<object>.Resolve(type_name);
        if (t == null) return null;
        MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != method_name) continue;
            Setup(methods[i]);
            return methods[i];
        }
        return null;
    }

    public override Raylib_cs.Color ED_GetTitleColor() => new(50, 90, 180, 255);
    public override string ED_GetTitle() => string.IsNullOrEmpty(method_name) ? "Call" : method_name;

    public override void Write(TTable tbl)
    {
        tbl.Set("type_name", type_name);
        tbl.Set("method_name", method_name);
    }

    public override void Read(TTable tbl)
    {
        type_name = tbl.get_String("type_name");
        method_name = tbl.get_String("method_name");
        Resolve();
    }
}

public class VN_ClassStatic : VisNode
{
    /*
     *  this gets a reference to a static class so you can call Static functions of it. Like "GMath.V3_Interp"
     */
}

public class VN_Hook_Enter : VisNode // Entry point for when overriding a "Virtual" function with [ScriptHook]
{
    public string hook_name = "OnBegin";

    public VN_Hook_Enter()
    {
        outputs = new[] { new VisPin { name = "then", type = typeof(VisExec) } };
    }

    public override Raylib_cs.Color ED_GetTitleColor() => new(180, 50, 50, 255);
    public override string ED_GetTitle() => string.IsNullOrEmpty(hook_name) ? "Event" : hook_name;

    public override void Write(TTable tbl) { tbl.Set("hook_name", hook_name); }
    public override void Read(TTable tbl) { hook_name = tbl.get_String("hook_name", "OnBegin"); }
}

public class VN_Hook_Return : VisNode // return point for when overriding a "Virtual" function with [ScriptHook]
{
    
}
