using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Engine.Core;
using Engine.Structs;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Engine.Sandbox;

/*
 * Lua scripting sandbox.
 * SCENES can have script placed right next to their scene file. E.G. "MyScene.lua" next to "MyScene.ImpScene". When that scene is loaded as current scene OR prefab instance, script is run to create a table/script instance.
 * "G.lua" can be placed in the root game directory (or any mod director) & is run automatically on startup.
 *
 * -- lua Globals ---
 * [ScriptCall] public static methods. [ImpClass(GlobalizeFunctions=true)] → Log_Info("x")
 * otherwise → TypeName.Method(args)
 */

public class Sandbox_Lua : ImpSandbox
{
    public static readonly string DEFAULT_SCRIPT_IMPCOMP = """
                                                            ---@class ImpScript : ImpComp
                                                            local a={}

                                                            function a:OnInit()
                                                            end

                                                            function a:OnBegin()
                                                            end

                                                            function a:OnUpdate(dt)
                                                            end

                                                            function a:OnEnd()
                                                            end

                                                            return a
                                                            """;

    Script? script;
    readonly Dictionary<string, DynValue> chunks = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<Type, TypeBind> binds = new();

    public override void Init()
    {
        script = new Script(CoreModules.Preset_SoftSandbox);
        script.Options.DebugPrint = s => Imp.Log_Info(s);
        UserData.RegisterType<LuaComp>();
        UserData.RegisterType<LuaAction>();
        UserData.RegisterType<LuaHooks>();
        UserData.RegisterType<ImpPlayer>(InteropAccessMode.HideMembers);
        script.Globals["Hooks"] = UserData.Create(new LuaHooks());
        RegisterStaticCalls();
    }

    public override void Shutdown()
    {
        chunks.Clear();
        script = null;
    }

    public override void RunGlobal(string path)
    {
        if (script == null || string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            script.DoString(File.ReadAllText(path), null, path);
        }
        catch (Exception ex)
        {
            Imp.Log_Error(LuaErr(path, ex));
        }
    }

    public override TScriptValue? RunInstance(string path, ImpComp owner)
    {
        if (script == null || owner == null || string.IsNullOrEmpty(path)) return null;
        try
        {
            if (!chunks.TryGetValue(path, out DynValue chunk))
            {
                if (!File.Exists(path)) return null;
                chunk = script.LoadString(File.ReadAllText(path), null, path);
                chunks[path] = chunk;
            }

            DynValue result = script.Call(chunk);
            if (result.Type != DataType.Table)
            {
                Imp.Log_Error($"Lua {path}: script must return a table");
                return null;
            }

            result.Table.Set("owner", UserData.Create(new LuaComp(owner)));
            Table mt = new(script);
            mt.Set("__index", DynValue.NewCallback((_, args) =>
            {
                if (args.Count < 2 || args[0].Type != DataType.Table) return DynValue.Nil;
                DynValue owner_dv = args[0].Table.RawGet("owner");
                if (owner_dv == null || owner_dv.Type != DataType.UserData || owner_dv.UserData.Object is not LuaComp lc)
                    return DynValue.Nil;
                return lc.Index(script, args[1], true);
            }));
            result.Table.MetaTable = mt;
            return new TScriptValue { sandbox = this, owner = owner, handle = result };
        }
        catch (Exception ex)
        {
            Imp.Log_Error(LuaErr(path, ex));
            return null;
        }
    }

    public override bool Has(TScriptValue inst, string name)
    {
        DynValue? table = TableOf(inst);
        if (table == null) return false;
        DynValue fn = table.Table.Get(name);
        return fn.Type == DataType.Function || fn.Type == DataType.ClrFunction;
    }

    public override void Call(TScriptValue inst, string name, params object[] args)
    {
        if (script == null || inst == null || string.IsNullOrEmpty(name)) return;
        DynValue? table = TableOf(inst);
        if (table == null) return;
        DynValue fn = table.Table.Get(name);
        if (fn.Type != DataType.Function && fn.Type != DataType.ClrFunction) return;

        try
        {
            DynValue[] call = new DynValue[(args?.Length ?? 0) + 1];
            call[0] = table;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                    call[i + 1] = ToLua(args[i]);
            }
            script.Call(fn, call);
        }
        catch (Exception ex)
        {
            Imp.Log_Error(LuaErr(name, ex));
        }
    }

    static DynValue? TableOf(TScriptValue inst)
    {
        return inst.handle is DynValue dv && dv.Type == DataType.Table ? dv : null;
    }

    static string LuaErr(string where, Exception ex)
    {
        if (ex is InterpreterException ie)
            return $"Lua {where}: {ie.DecoratedMessage ?? ie.Message}";
        return $"Lua {where}: {ex.Message}";
    }

    void RegisterStaticCalls()
    {
        HashSet<string> globals = new(StringComparer.Ordinal);
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            foreach (Type t in types)
            {
                if (t == null || t.IsGenericTypeDefinition) continue;
                if ((t.Namespace ?? "").StartsWith("Editor_Vibe")) continue;

                bool globalize = t.GetCustomAttribute<ImpClassAttribute>()?.GlobalizeFunctions == true;
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!m.IsDefined(typeof(ScriptCallAttribute), true)) continue;
                    if (m.IsGenericMethod) continue;
                    bool byref = false;
                    foreach (ParameterInfo p in m.GetParameters())
                    {
                        if (p.ParameterType.IsByRef) { byref = true; break; }
                    }
                    if (byref) continue;

                    DynValue cb = DynValue.NewCallback(MakeCallback(null, m));
                    if (globalize)
                    {
                        if (!globals.Add(m.Name))
                        {
                            Imp.Log_Error($"ScriptCall global collision: {m.Name} ({t.Name})");
                            continue;
                        }
                        script!.Globals[m.Name] = cb;
                    }
                    else
                    {
                        DynValue existing = script!.Globals.Get(t.Name);
                        Table table;
                        if (existing.Type == DataType.Table)
                            table = existing.Table;
                        else
                        {
                            table = new Table(script);
                            script.Globals[t.Name] = table;
                        }
                        table[m.Name] = cb;
                    }
                }
            }
        }
    }

    Func<ScriptExecutionContext, CallbackArguments, DynValue> MakeCallback(ImpComp? target, MethodInfo method)
    {
        ParameterInfo[] ps = method.GetParameters();
        return (ctx, args) =>
        {
            try
            {
                int start = 0;
                if (args.Count > 0)
                {
                    DataType t0 = args[0].Type;
                    if (t0 == DataType.Table) start = 1;
                    else if (t0 == DataType.UserData && args[0].UserData.Object is LuaComp) start = 1;
                }

                object?[] invoke = new object?[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    int ai = start + i;
                    if (ai < args.Count && !args[ai].IsNil())
                        invoke[i] = ToClr(args[ai], ps[i].ParameterType);
                    else if (ps[i].HasDefaultValue)
                        invoke[i] = ps[i].DefaultValue;
                    else if (ps[i].ParameterType.IsValueType)
                        invoke[i] = Activator.CreateInstance(ps[i].ParameterType);
                    else
                        invoke[i] = null;
                }

                object? result = method.Invoke(target, invoke);
                return ToLua(result);
            }
            catch (Exception ex)
            {
                Imp.Log_Error(LuaErr(method.Name, ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex));
                return DynValue.Nil;
            }
        };
    }

    DynValue ToLua(object? value)
    {
        if (value == null || script == null) return DynValue.Nil;
        if (value is ImpComp comp) return UserData.Create(new LuaComp(comp));
        if (value is TLabel lab) return DynValue.NewString(lab.Value);
        if (value is Vector3 v)
        {
            Table t = new(script);
            t["X"] = v.X; t["Y"] = v.Y; t["Z"] = v.Z;
            return DynValue.NewTable(t);
        }
        try { return DynValue.FromObject(script, value); }
        catch { return DynValue.NewString(value.ToString()); }
    }

    object? ToClr(DynValue v, Type t)
    {
        if (v == null || v.IsNil()) return t.IsValueType ? Activator.CreateInstance(t) : null;
        if (t == typeof(string)) return v.CastToString();
        if (t == typeof(bool)) return v.CastToBool();
        if (t == typeof(int) || t == typeof(byte) || t == typeof(float) || t == typeof(double) || t == typeof(long))
        {
            double? n = v.CastToNumber();
            if (n == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            return Convert.ChangeType(n.Value, t, CultureInfo.InvariantCulture);
        }
        if (typeof(ImpComp).IsAssignableFrom(t) && v.Type == DataType.UserData && v.UserData.Object is LuaComp lc)
            return lc.Comp;
        if (t == typeof(TLabel)) return TLabel.From(v.CastToString());
        if (t == typeof(Vector3))
        {
            if (v.Type == DataType.Table)
            {
                Table tb = v.Table;
                return new Vector3(
                    (float)tb.Get("X").CastToNumber().GetValueOrDefault(),
                    (float)tb.Get("Y").CastToNumber().GetValueOrDefault(),
                    (float)tb.Get("Z").CastToNumber().GetValueOrDefault());
            }
            return Vector3.Zero;
        }
        try { return v.ToObject(t); }
        catch { return t.IsValueType ? Activator.CreateInstance(t) : null; }
    }

    static TypeBind BindOf(Type t)
    {
        if (binds.TryGetValue(t, out TypeBind? b)) return b;
        b = new TypeBind();
        for (Type? cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
        {
            foreach (FieldInfo f in cur.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsAction(f.FieldType) && !b.actions.ContainsKey(f.Name)) b.actions[f.Name] = f;
                if (!f.IsDefined(typeof(ImpVarAttribute), true)) continue;
                if (!IsPrim(f.FieldType)) continue;
                if (!b.vars.ContainsKey(f.Name)) b.vars[f.Name] = f;
            }
            foreach (PropertyInfo p in cur.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!p.IsDefined(typeof(ImpVarAttribute), true)) continue;
                if (!IsPrim(p.PropertyType)) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                if (!b.vars.ContainsKey(p.Name)) b.vars[p.Name] = p;
            }
        }
        foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!m.IsDefined(typeof(ScriptCallAttribute), true)) continue;
            if (m.IsGenericMethod || m.IsStatic) continue;
            b.calls[m.Name] = m;
        }
        binds[t] = b;
        return b;
    }

    static TypeBind BindStatic(Type t)
    {
        if (static_binds.TryGetValue(t, out TypeBind? b)) return b;
        b = new TypeBind();
        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (IsAction(f.FieldType)) b.actions[f.Name] = f;
        }
        static_binds[t] = b;
        return b;
    }

    static bool IsPrim(Type t) =>
        t == typeof(bool) || t == typeof(int) || t == typeof(float) || t == typeof(double) ||
        t == typeof(string) || t == typeof(byte);

    static bool IsAction(Type t)
    {
        if (t == typeof(Action)) return true;
        if (t == null || !t.IsGenericType) return false;
        Type g = t.GetGenericTypeDefinition();
        return g == typeof(Action<>) || g == typeof(Action<,>) || g == typeof(Action<,,>) || g == typeof(Action<,,,>);
    }

    static readonly Dictionary<Type, TypeBind> static_binds = new();
    static readonly ConditionalWeakTable<object, Dictionary<FieldInfo, Dictionary<Closure, Delegate>>> lua_dels = new();

    static Dictionary<Closure, Delegate> MapFor(object key, FieldInfo f)
    {
        if (!lua_dels.TryGetValue(key, out Dictionary<FieldInfo, Dictionary<Closure, Delegate>>? per_field))
        {
            per_field = new();
            lua_dels.Add(key, per_field);
        }
        if (!per_field.TryGetValue(f, out Dictionary<Closure, Delegate>? map))
        {
            map = new();
            per_field[f] = map;
        }
        return map;
    }

    static DynValue FnArg(CallbackArguments args)
    {
        for (int i = 0; i < args.Count; i++)
            if (args[i].Type == DataType.Function) return args[i];
        return DynValue.Nil;
    }

    static bool SetActionIndex(FieldInfo field, object? target, DynValue value)
    {
        if (value.Type == DataType.UserData && value.UserData.Object is LuaAction) return true;
        if (value.Type == DataType.Function)
        {
            Imp.Log_Error($"Lua: cannot assign function to {field.Name}; use `{field.Name}.Add(fn)` / `{field.Name}.Remove(fn)`");
            return true;
        }
        return false;
    }

    Delegate? MakeLuaDelegate(Type action_type, DynValue fn)
    {
        Type[] gens = action_type.IsGenericType ? action_type.GetGenericArguments() : Type.EmptyTypes;
        if (gens.Length > 4)
        {
            Imp.Log_Error($"Lua: Action arity {gens.Length} not supported ({action_type.Name})");
            return null;
        }
        LuaFnInvoke inv = new() { lua = this, fn = fn };
        MethodInfo? m = typeof(LuaFnInvoke).GetMethod("Invoke" + gens.Length);
        if (m == null) return null;
        if (gens.Length > 0) m = m.MakeGenericMethod(gens);
        return Delegate.CreateDelegate(action_type, inv, m);
    }

    class TypeBind
    {
        public readonly Dictionary<string, MemberInfo> vars = new(StringComparer.Ordinal);
        public readonly Dictionary<string, MethodInfo> calls = new(StringComparer.Ordinal);
        public readonly Dictionary<string, FieldInfo> actions = new(StringComparer.Ordinal);
    }

    class LuaFnInvoke
    {
        public Sandbox_Lua lua = null!;
        public DynValue fn;

        public void Invoke0() => Call();
        public void Invoke1<T0>(T0 a0) => Call(a0);
        public void Invoke2<T0, T1>(T0 a0, T1 a1) => Call(a0, a1);
        public void Invoke3<T0, T1, T2>(T0 a0, T1 a1, T2 a2) => Call(a0, a1, a2);
        public void Invoke4<T0, T1, T2, T3>(T0 a0, T1 a1, T2 a2, T3 a3) => Call(a0, a1, a2, a3);

        void Call(params object?[] args)
        {
            if (lua?.script == null) return;
            try
            {
                DynValue[] call = new DynValue[args.Length];
                for (int i = 0; i < args.Length; i++)
                    call[i] = lua.ToLua(args[i]);
                lua.script.Call(fn, call);
            }
            catch (Exception ex)
            {
                Imp.Log_Error(LuaErr("Action", ex));
            }
        }
    }

    public class LuaComp : IUserDataType
    {
        public readonly ImpComp Comp;
        public LuaComp(ImpComp comp) { Comp = comp; }

        public DynValue Index(Script script, DynValue index, bool isDirectIndexing)
        {
            string? key = index.String;
            if (string.IsNullOrEmpty(key) || Comp == null) return DynValue.Nil;

            if (key == "parent")
                return Comp.parent != null ? UserData.Create(new LuaComp(Comp.parent)) : DynValue.Nil;

            if (key == "find")
            {
                return DynValue.NewCallback((_, args) =>
                {
                    int i = 0;
                    if (args.Count > 0 && args[0].Type == DataType.UserData && args[0].UserData.Object is LuaComp)
                        i = 1;
                    string name = i < args.Count ? args[i].CastToString() ?? "" : "";
                    ImpComp? found = Find(Comp, name);
                    return found != null ? UserData.Create(new LuaComp(found)) : DynValue.Nil;
                });
            }

            TypeBind b = BindOf(Comp.GetType());
            if (b.vars.TryGetValue(key, out MemberInfo? member))
            {
                object? val = member is FieldInfo f ? f.GetValue(Comp) : ((PropertyInfo)member).GetValue(Comp);
                if (val == null) return DynValue.Nil;
                if (val is string s) return DynValue.NewString(s);
                if (val is bool bo) return DynValue.NewBoolean(bo);
                return DynValue.NewNumber(Convert.ToDouble(val, CultureInfo.InvariantCulture));
            }

            if (b.calls.TryGetValue(key, out MethodInfo? method))
            {
                Sandbox_Lua? lua = ImpSandbox.current as Sandbox_Lua;
                if (lua == null) return DynValue.Nil;
                return DynValue.NewCallback(lua.MakeCallback(Comp, method));
            }

            if (b.actions.TryGetValue(key, out FieldInfo? action))
                return UserData.Create(new LuaAction(Comp, action));

            return DynValue.Nil;
        }

        public bool SetIndex(Script script, DynValue index, DynValue value, bool isDirectIndexing)
        {
            string? key = index.String;
            if (string.IsNullOrEmpty(key) || Comp == null) return false;
            TypeBind b = BindOf(Comp.GetType());
            if (b.actions.TryGetValue(key, out FieldInfo? action))
                return SetActionIndex(action, Comp, value);
            if (!b.vars.TryGetValue(key, out MemberInfo? member)) return false;
            Type mt = member is FieldInfo f0 ? f0.FieldType : ((PropertyInfo)member).PropertyType;
            object? converted;
            if (mt == typeof(string)) converted = value.IsNil() ? "" : value.CastToString();
            else if (mt == typeof(bool)) converted = value.CastToBool();
            else
            {
                double? n = value.CastToNumber();
                converted = n == null ? Activator.CreateInstance(mt) : Convert.ChangeType(n.Value, mt, CultureInfo.InvariantCulture);
            }
            if (member is FieldInfo f) f.SetValue(Comp, converted);
            else ((PropertyInfo)member).SetValue(Comp, converted);
            return true;
        }

        public DynValue MetaIndex(Script script, string metaname) => null!;

        static ImpComp? Find(ImpComp root, string name)
        {
            foreach (ImpComp c in root.children)
            {
                if (c.name == name) return c;
                ImpComp? n = Find(c, name);
                if (n != null) return n;
            }
            return null;
        }
    }

    public class LuaAction : IUserDataType
    {
        readonly object? target;
        readonly FieldInfo field;

        public LuaAction(object? target, FieldInfo field)
        {
            this.target = target;
            this.field = field;
        }

        public DynValue Index(Script script, DynValue index, bool isDirectIndexing)
        {
            string? key = index.String;
            if (string.Equals(key, "Add", StringComparison.OrdinalIgnoreCase))
                return DynValue.NewCallback((_, args) => { Hook(true, args); return DynValue.Nil; });
            if (string.Equals(key, "Remove", StringComparison.OrdinalIgnoreCase))
                return DynValue.NewCallback((_, args) => { Hook(false, args); return DynValue.Nil; });
            return DynValue.Nil;
        }
        public bool SetIndex(Script script, DynValue index, DynValue value, bool isDirectIndexing) => false;
        public DynValue MetaIndex(Script script, string metaname) => null!;

        void Hook(bool add, CallbackArguments args)
        {
            DynValue fn = FnArg(args);
            if (fn.IsNil() || fn.Type != DataType.Function)
            {
                Imp.Log_Error($"Lua: {field.Name}.{(add ? "Add" : "Remove")} expects a function");
                return;
            }
            Sandbox_Lua? lua = ImpSandbox.current as Sandbox_Lua;
            if (lua == null) return;

            object map_key = target ?? field.DeclaringType!;
            Dictionary<Closure, Delegate> map = MapFor(map_key, field);
            Closure closure = fn.Function;
            Delegate? existing = field.GetValue(target) as Delegate;

            if (add)
            {
                if (map.ContainsKey(closure)) return;
                Delegate? wrapped = lua.MakeLuaDelegate(field.FieldType, fn);
                if (wrapped == null) return;
                map[closure] = wrapped;
                field.SetValue(target, Delegate.Combine(existing, wrapped));
            }
            else
            {
                if (!map.TryGetValue(closure, out Delegate? wrapped)) return;
                map.Remove(closure);
                field.SetValue(target, Delegate.Remove(existing, wrapped));
            }
        }
    }

    public class LuaHooks : IUserDataType
    {
        public DynValue Index(Script script, DynValue index, bool isDirectIndexing)
        {
            string? key = index.String;
            if (string.IsNullOrEmpty(key)) return DynValue.Nil;
            TypeBind b = BindStatic(typeof(Hooks));
            if (b.actions.TryGetValue(key, out FieldInfo? action))
                return UserData.Create(new LuaAction(null, action));
            return DynValue.Nil;
        }

        public bool SetIndex(Script script, DynValue index, DynValue value, bool isDirectIndexing)
        {
            string? key = index.String;
            if (string.IsNullOrEmpty(key)) return false;
            TypeBind b = BindStatic(typeof(Hooks));
            if (b.actions.TryGetValue(key, out FieldInfo? action))
                return SetActionIndex(action, null, value);
            return false;
        }

        public DynValue MetaIndex(Script script, string metaname) => null!;
    }
}
