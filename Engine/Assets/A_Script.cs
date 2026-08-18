using System.Globalization;
using System.Numerics;
using System.Reflection;
using ImperiumEngine.Script;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Assets;

public enum EScriptNodeType
{
    VoidOverride, // node for the starting point of a function override that returns void (like UE Event)
    Func, // node for a function call
    FuncInOut, // node for inside a custom function, either input or output
    Var, // node for a variable get/set
    Async, // node for an async function call
    Branch // node for a function that is NOT async but has multiple execution outputs
}


public enum EImpVarEdit
{
    None, ReadOnly, ReadWrite
}

public enum EImpVarInspect
{
    ReadOnly,
    ReadWrite,
    InspectorEdit,
    InspectorReadOnly,
}

public struct TScriptVar
{
    public string name;
    public EImpVarEdit edit;
    public EImpVarInspect inspect;
    public string type_name;
}

// Default on an unconnected value input. Stored as invariant text so File_JSON can round-trip it.
public class TPulsePinValue
{
    public string name = "";
    public string type_name = "";
    public string value = "";
}

// Editor/serialized Pulse node. Live C2_GraphNode.user_data points at this instance.
public class TPulseNode
{
    public Guid id;
    public EScriptNodeType kind;
    public string node_class = ""; // SN_* class that describes this node. Empty on nodes saved before it existed.
    public string member = "";
    public string target_type = "";
    public bool is_set;
    public Vector2 position;
    public List<TPulsePinValue> pin_values = new();

    public object Pin_Get(string name, Type type)
    {
        if (pin_values != null && !string.IsNullOrEmpty(name))
        {
            for (int i = 0; i < pin_values.Count; i++)
            {
                TPulsePinValue p = pin_values[i];
                if (p != null && p.name == name)
                {
                    return Pulse.ValueFromText(type, p.value);
                }
            }
        }
        return Pulse.ValueDefault(type);
    }

    public void Pin_Set(string name, Type type, object value)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        if (pin_values == null)
        {
            pin_values = new List<TPulsePinValue>();
        }
        string tn = "";
        if (type != null)
        {
            tn = type.Name;
        }
        string text = Pulse.ValueToText(value);
        for (int i = 0; i < pin_values.Count; i++)
        {
            TPulsePinValue p = pin_values[i];
            if (p != null && p.name == name)
            {
                p.type_name = tn;
                p.value = text;
                return;
            }
        }
        pin_values.Add(new TPulsePinValue
        {
            name = name,
            type_name = tn,
            value = text,
        });
    }

    public TPulseNode Copy()
    {
        TPulseNode n = new TPulseNode
        {
            id = id,
            kind = kind,
            node_class = node_class,
            member = member,
            target_type = target_type,
            is_set = is_set,
            position = position,
        };
        if (pin_values != null)
        {
            for (int i = 0; i < pin_values.Count; i++)
            {
                TPulsePinValue p = pin_values[i];
                if (p == null)
                {
                    continue;
                }
                n.pin_values.Add(new TPulsePinValue
                {
                    name = p.name,
                    type_name = p.type_name,
                    value = p.value,
                });
            }
        }
        return n;
    }
}

public class TPulseFunc
{
    public Type owner;
    public MethodInfo method;
    public string name;
    public bool is_override;
    public bool is_exec;
    public ParameterInfo[] pars;
    public Type return_type;
}

public class TPulseVar
{
    public Type owner;
    public MemberInfo member;
    public string name;
    public Type type;
    public bool can_set;
}

// Pin-type colors / ids used by the graph widget. Exec is always 0.
public static class Pulse
{
    public const int ExecId = 0;

    static readonly Dictionary<Type, int> _ids = new();
    static readonly Dictionary<int, Type> _types = new();
    static int _next = 1;

    public static int TypeId(Type t)
    {
        if (t == null)
        {
            return ExecId;
        }
        if (_ids.TryGetValue(t, out int id))
        {
            return id;
        }
        id = _next;
        _next += 1;
        _ids[t] = id;
        _types[id] = t;
        return id;
    }

    public static Type TypeFromId(int id)
    {
        if (id == ExecId)
        {
            return null;
        }
        _types.TryGetValue(id, out Type t);
        return t;
    }

    public static Color TypeColor(Type t)
    {
        if (t == null)
        {
            return new Color(230, 230, 230, 255);
        }
        if (t == typeof(bool))
        {
            return new Color(220, 60, 60, 255);
        }
        if (t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(byte))
        {
            return new Color(80, 200, 180, 255);
        }
        if (t == typeof(float) || t == typeof(double))
        {
            return new Color(80, 200, 100, 255);
        }
        if (t == typeof(string))
        {
            return new Color(200, 80, 180, 255);
        }
        if (t == typeof(Vector2))
        {
            return new Color(220, 180, 60, 255);
        }
        if (t == typeof(Vector3))
        {
            return new Color(220, 160, 40, 255);
        }
        if (t == typeof(Color))
        {
            return new Color(180, 80, 220, 255);
        }
        if (typeof(ImpAsset).IsAssignableFrom(t))
        {
            return new Color(110, 200, 200, 255);
        }
        if (typeof(ImpComp).IsAssignableFrom(t))
        {
            return new Color(80, 140, 220, 255);
        }
        if (t.IsEnum)
        {
            return new Color(80, 200, 180, 255);
        }
        if (t.IsClass || t.IsInterface)
        {
            return new Color(90, 140, 200, 255);
        }
        return new Color(140, 140, 150, 255);
    }

    public static bool IsObjectType(Type t)
    {
        if (t == null)
        {
            return false;
        }
        if (t.IsPrimitive || t.IsEnum)
        {
            return false;
        }
        if (t == typeof(string) || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Color))
        {
            return false;
        }
        return t.IsClass || t.IsInterface;
    }

    public static bool CanEditDefault(Type t)
    {
        if (t == null)
        {
            return false;
        }
        if (t == typeof(string) || t == typeof(bool))
        {
            return true;
        }
        if (t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(byte)
            || t == typeof(float) || t == typeof(double))
        {
            return true;
        }
        if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Color))
        {
            return true;
        }
        if (t.IsEnum)
        {
            return true;
        }
        return false;
    }

    public static object ValueDefault(Type t)
    {
        if (t == null)
        {
            return null;
        }
        if (t == typeof(string))
        {
            return "";
        }
        if (t.IsValueType)
        {
            return Activator.CreateInstance(t);
        }
        return null;
    }

    public static string ValueToText(object v)
    {
        if (v == null)
        {
            return "";
        }
        if (v is string s)
        {
            return s;
        }
        if (v is bool b)
        {
            if (b)
            {
                return "true";
            }
            return "false";
        }
        if (v is float f)
        {
            return f.ToString(CultureInfo.InvariantCulture);
        }
        if (v is double d)
        {
            return d.ToString(CultureInfo.InvariantCulture);
        }
        if (v is int i)
        {
            return i.ToString(CultureInfo.InvariantCulture);
        }
        if (v is uint ui)
        {
            return ui.ToString(CultureInfo.InvariantCulture);
        }
        if (v is long l)
        {
            return l.ToString(CultureInfo.InvariantCulture);
        }
        if (v is byte by)
        {
            return by.ToString(CultureInfo.InvariantCulture);
        }
        if (v is Vector2 v2)
        {
            return v2.X.ToString(CultureInfo.InvariantCulture) + "," + v2.Y.ToString(CultureInfo.InvariantCulture);
        }
        if (v is Vector3 v3)
        {
            return v3.X.ToString(CultureInfo.InvariantCulture) + "," + v3.Y.ToString(CultureInfo.InvariantCulture)
                + "," + v3.Z.ToString(CultureInfo.InvariantCulture);
        }
        if (v is Vector4 v4)
        {
            return v4.X.ToString(CultureInfo.InvariantCulture) + "," + v4.Y.ToString(CultureInfo.InvariantCulture)
                + "," + v4.Z.ToString(CultureInfo.InvariantCulture) + "," + v4.W.ToString(CultureInfo.InvariantCulture);
        }
        if (v is Color c)
        {
            return c.R.ToString(CultureInfo.InvariantCulture) + "," + c.G.ToString(CultureInfo.InvariantCulture)
                + "," + c.B.ToString(CultureInfo.InvariantCulture) + "," + c.A.ToString(CultureInfo.InvariantCulture);
        }
        return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }

    public static object ValueFromText(Type t, string text)
    {
        if (t == null)
        {
            return null;
        }
        if (text == null)
        {
            text = "";
        }
        if (t == typeof(string))
        {
            return text;
        }
        if (t == typeof(bool))
        {
            if (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
        if (t == typeof(int))
        {
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
            return n;
        }
        if (t == typeof(uint))
        {
            uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint n);
            return n;
        }
        if (t == typeof(long))
        {
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n);
            return n;
        }
        if (t == typeof(byte))
        {
            byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte n);
            return n;
        }
        if (t == typeof(float))
        {
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float n);
            return n;
        }
        if (t == typeof(double))
        {
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double n);
            return n;
        }
        if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Color))
        {
            string[] parts = text.Split(',');
            float a = 0f;
            float b = 0f;
            float c = 0f;
            float d = 0f;
            if (parts.Length > 0)
            {
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a);
            }
            if (parts.Length > 1)
            {
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b);
            }
            if (parts.Length > 2)
            {
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c);
            }
            if (parts.Length > 3)
            {
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out d);
            }
            if (t == typeof(Vector2))
            {
                return new Vector2(a, b);
            }
            if (t == typeof(Vector3))
            {
                return new Vector3(a, b, c);
            }
            if (t == typeof(Vector4))
            {
                return new Vector4(a, b, c, d);
            }
            return new Color(
                (byte)Math.Clamp(MathF.Round(a), 0, 255),
                (byte)Math.Clamp(MathF.Round(b), 0, 255),
                (byte)Math.Clamp(MathF.Round(c), 0, 255),
                (byte)Math.Clamp(MathF.Round(d), 0, 255));
        }
        if (t.IsEnum)
        {
            try
            {
                return Enum.Parse(t, text, true);
            }
            catch
            {
                return Activator.CreateInstance(t);
            }
        }
        return ValueDefault(t);
    }

    public static List<TPulseFunc> Overrides(Type type)
    {
        return Methods(type, true);
    }

    public static List<TPulseFunc> Calls(Type type)
    {
        return Methods(type, false);
    }

    public static List<TPulseVar> Vars(Type type)
    {
        List<TPulseVar> list = new();
        if (type == null)
        {
            return list;
        }
        HashSet<string> seen = new(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            FieldInfo[] fields = t.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                ImpVarAttribute attr = f.GetCustomAttribute<ImpVarAttribute>();
                if (attr == null || attr.Hidden)
                {
                    continue;
                }
                // Only vars that opted into scripting get a SN_VarGet / SN_VarSet.
                if (attr.Edit == EImpVarEdit.None)
                {
                    continue;
                }
                if (!seen.Add(f.Name))
                {
                    continue;
                }
                bool writable = attr.Edit == EImpVarEdit.ReadWrite && !f.IsInitOnly;
                list.Add(new TPulseVar
                {
                    owner = t,
                    member = f,
                    name = f.Name,
                    type = f.FieldType,
                    can_set = writable,
                });
            }
            PropertyInfo[] props = t.GetProperties(flags);
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                if (!p.CanRead || p.GetIndexParameters().Length != 0)
                {
                    continue;
                }
                ImpVarAttribute attr = p.GetCustomAttribute<ImpVarAttribute>();
                if (attr == null || attr.Hidden)
                {
                    continue;
                }
                if (attr.Edit == EImpVarEdit.None)
                {
                    continue;
                }
                if (!seen.Add(p.Name))
                {
                    continue;
                }
                bool writable = attr.Edit == EImpVarEdit.ReadWrite && p.CanWrite;
                list.Add(new TPulseVar
                {
                    owner = t,
                    member = p,
                    name = p.Name,
                    type = p.PropertyType,
                    can_set = writable,
                });
            }
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static TPulseFunc FindOverride(Type type, string name)
    {
        return FindMethod(type, name, true);
    }

    public static TPulseFunc FindCall(Type type, string name)
    {
        return FindMethod(type, name, false);
    }

    public static TPulseVar FindVar(Type type, string name)
    {
        List<TPulseVar> list = Vars(type);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].name == name)
            {
                return list[i];
            }
        }
        return null;
    }

    static List<TPulseFunc> Methods(Type type, bool ov)
    {
        List<TPulseFunc> list = new();
        if (type == null)
        {
            return list;
        }
        HashSet<string> seen = new(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            MethodInfo[] methods = t.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];
                if (m.IsSpecialName)
                {
                    continue;
                }
                bool is_ov = m.GetCustomAttribute<ScriptOverrideAttribute>() != null;
                bool is_call = m.GetCustomAttribute<ScriptCallAttribute>() != null;
                if (ov)
                {
                    if (!is_ov)
                    {
                        continue;
                    }
                }
                else if (!is_call)
                {
                    continue;
                }
                if (!seen.Add(m.Name))
                {
                    continue;
                }
                list.Add(FromMethod(t, m, ov));
            }
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    static TPulseFunc FindMethod(Type type, string name, bool ov)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        List<TPulseFunc> list = Methods(type, ov);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].name == name)
            {
                return list[i];
            }
        }
        return null;
    }

    static TPulseFunc FromMethod(Type owner, MethodInfo m, bool ov)
    {
        Type ret = m.ReturnType;
        bool exec = ov || ret == typeof(void);
        return new TPulseFunc
        {
            owner = owner,
            method = m,
            name = m.Name,
            is_override = ov,
            is_exec = exec,
            pars = m.GetParameters(),
            return_type = ret,
        };
    }
}

// PULSE is Imperium Engine's visual scripting language.
[AssetColor(110, 200, 200)]
public class A_Script : ImpAsset
{
    [ImpVar] public TClass<Object> parent_type;

    [ImpVar(Hidden = true)] public List<TPulseNode> nodes = new();
    [ImpVar(Hidden = true)] public List<TFlowConnection> connections = new();
    [ImpVar] public List<TScriptVar> vars = new();

    // Compiled form. Plain fields — never serialized, so a script loaded off disk always
    // compiles once before it runs.
    public TScriptProgram compiled;
    public bool compile_dirty = true;

    // Compiles if needed. Called when a script is instanced to be run.
    public TScriptProgram Program_Get()
    {
        if (compiled == null || compile_dirty)
        {
            return Compile();
        }
        return compiled;
    }

    // Turn the authored nodes + wires into runnable ScriptNode instances.
    public TScriptProgram Compile()
    {
        TScriptProgram p = new TScriptProgram();
        p.script = this;
        Type parent = ParentType_Get();
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TPulseNode n = nodes[i];
                if (n == null)
                {
                    continue;
                }
                Type ctx = TClass<Object>.Resolve(n.target_type);
                if (ctx == null)
                {
                    ctx = parent;
                }
                ScriptNode sn = ScriptNodes.Create(n, ctx, this);
                if (sn == null)
                {
                    p.errors.Add("Node '" + n.member + "': unknown class '" + n.node_class + "'.");
                    continue;
                }
                p.nodes.Add(sn);
            }
        }
        if (connections != null)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                p.links.Add(connections[i]);
            }
        }
        // Check first: a node may rebind to another type, which changes its pin types.
        for (int i = 0; i < p.nodes.Count; i++)
        {
            p.nodes[i].Compile_Check(p, p.errors);
        }
        for (int i = 0; i < p.nodes.Count; i++)
        {
            ScriptNode sn = p.nodes[i];
            sn.slots.Clear();
            sn.Slots_Build(sn.slots);
        }
        compiled = p;
        compile_dirty = false;
        return p;
    }

    public Type ParentType_Get()
    {
        Type t = parent_type.Get();
        if (t != null)
        {
            return t;
        }
        return typeof(object);
    }

    public TPulseNode Node_Find(Guid id)
    {
        if (nodes == null)
        {
            return null;
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            TPulseNode n = nodes[i];
            if (n != null && n.id == id)
            {
                return n;
            }
        }
        return null;
    }

    public TPulseNode Node_FindOverride(string member)
    {
        if (nodes == null || string.IsNullOrEmpty(member))
        {
            return null;
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            TPulseNode n = nodes[i];
            if (n != null && n.kind == EScriptNodeType.VoidOverride && n.member == member)
            {
                return n;
            }
        }
        return null;
    }

    // Type of a script-local var. These are not reflected off parent_type, so var nodes fall back to this.
    public Type Var_TypeOf(string name)
    {
        if (vars == null || string.IsNullOrEmpty(name))
        {
            return null;
        }
        for (int i = 0; i < vars.Count; i++)
        {
            if (vars[i].name == name)
            {
                return TClass<Object>.Resolve(vars[i].type_name);
            }
        }
        return null;
    }

    public override string File_GetExtension()
    {
        return "ImpScript";
    }

    public override ImpAsset Clone()
    {
        A_Script copy = base.Clone() as A_Script;
        if (copy == null)
        {
            return null;
        }
        copy.nodes = new List<TPulseNode>();
        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                TPulseNode n = nodes[i];
                if (n != null)
                {
                    copy.nodes.Add(n.Copy());
                }
            }
        }
        copy.connections = new List<TFlowConnection>();
        if (connections != null)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                copy.connections.Add(connections[i]);
            }
        }
        copy.vars = new List<TScriptVar>();
        if (vars != null)
        {
            for (int i = 0; i < vars.Count; i++)
            {
                copy.vars.Add(vars[i]);
            }
        }
        // The copy owns its own nodes, so it must compile its own program (PIE runs the copy).
        copy.compiled = null;
        copy.compile_dirty = true;
        return copy;
    }
}


