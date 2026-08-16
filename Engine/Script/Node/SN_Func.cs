using System.Reflection;
using ImperiumEngine.Assets;
using Raylib_cs;

namespace ImperiumEngine.Script.Node;

// A [PulseCall] on the target type. Instanced per call, so it is never available on its own.
public class SN_Func : ScriptNode
{
    public bool is_execute; // forced true for any function with a void return type. TRUE = Exec function (called when entered). FALSE= Pure Function (called when referenced)

    TPulseFunc _func;

    public override void Bind(TPulseNode node, Type ctx)
    {
        base.Bind(node, ctx);
        _func = Pulse.FindCall(context, member);
        is_execute = _func == null || _func.is_exec;
    }

    public override Color GetNode_Color()
    {
        if (is_execute)
        {
            return new Color(70, 120, 180, 255);
        }
        return new Color(70, 160, 90, 255);
    }

    public override void Slots_Build(List<TScriptSlot> slots)
    {
        if (is_execute)
        {
            slots.Add(new TScriptSlot { enable_left = true, enable_right = true });
        }

        if (Slot_Target() >= 0)
        {
            Type target_t = context;
            if (_func != null && _func.owner != null)
            {
                target_t = _func.owner;
            }
            slots.Add(new TScriptSlot { enable_left = true, type_left = target_t, name_left = "Target" });
        }

        if (_func == null)
        {
            return;
        }
        ParameterInfo[] pars = _func.pars;
        for (int i = 0; i < pars.Length; i++)
        {
            string n = pars[i].Name;
            if (string.IsNullOrEmpty(n))
            {
                n = "arg" + i;
            }
            slots.Add(new TScriptSlot
            {
                enable_left = true,
                type_left = pars[i].ParameterType,
                name_left = n,
            });
        }

        // Pure nodes return through a pin. Share the first row if its output side is free.
        if (!is_execute && _func.return_type != null && _func.return_type != typeof(void))
        {
            if (slots.Count > 0 && !slots[0].enable_right)
            {
                slots[0].enable_right = true;
                slots[0].type_right = _func.return_type;
                slots[0].name_right = "Return";
            }
            else
            {
                slots.Add(new TScriptSlot
                {
                    enable_right = true,
                    type_right = _func.return_type,
                    name_right = "Return",
                });
            }
        }
    }

    // Row holding the Target pin, or -1 for a static call.
    int Slot_Target()
    {
        if (_func != null && _func.method != null && _func.method.IsStatic)
        {
            return -1;
        }
        if (is_execute)
        {
            return 1;
        }
        return 0;
    }

    // First row holding a parameter.
    int Slot_Params()
    {
        int s = 0;
        if (is_execute)
        {
            s += 1;
        }
        if (Slot_Target() >= 0)
        {
            s += 1;
        }
        return s;
    }

    public override void Compile_Check(TScriptProgram program, List<string> errors)
    {
        if (string.IsNullOrEmpty(member))
        {
            errors.Add("Call node has no member.");
            return;
        }
        if (_func == null || _func.method == null)
        {
            errors.Add("Call '" + member + "' not found on " + context.Name + ".");
            return;
        }
        if (_func.method.IsStatic || script == null)
        {
            return;
        }
        // A node can point at a type the script is no longer based on — scene scripts moved from
        // ImpScene to the root comp class. Rebind to the script's own type, unless Target is wired
        // to something else, in which case the author meant that type.
        Type self_t = script.ParentType_Get();
        if (_func.owner == null || _func.owner.IsAssignableFrom(self_t))
        {
            return;
        }
        if (program != null && program.Input_IsWired(guid, Slot_Target()))
        {
            return;
        }
        TPulseFunc alt = Pulse.FindCall(self_t, member);
        if (alt == null)
        {
            errors.Add("Call '" + member + "' targets " + _func.owner.Name + ", which "
                + self_t.Name + " is not — wire a Target.");
            return;
        }
        _func = alt;
        context = self_t;
        is_execute = alt.is_exec;
    }

    public override int Exec_Run(ImpScriptVM vm)
    {
        Invoke(vm);
        return 0;
    }

    public override object Value_Get(ImpScriptVM vm, int slot)
    {
        return Invoke(vm);
    }

    object Invoke(ImpScriptVM vm)
    {
        if (_func == null || _func.method == null)
        {
            return null;
        }
        object target = null;
        int ts = Slot_Target();
        if (ts >= 0)
        {
            target = vm.Target_Get(this, ts);
            if (target == null)
            {
                Console.WriteLine("[Pulse] " + member + ": no target.");
                return null;
            }
            if (_func.method.DeclaringType != null && !_func.method.DeclaringType.IsInstanceOfType(target))
            {
                Console.WriteLine("[Pulse] " + member + ": target is " + target.GetType().Name
                    + ", expected " + _func.method.DeclaringType.Name + ".");
                return null;
            }
        }
        ParameterInfo[] pars = _func.pars;
        object[] args = new object[pars.Length];
        int slot = Slot_Params();
        for (int i = 0; i < pars.Length; i++)
        {
            args[i] = ImpScriptVM.Value_As(vm.Input_Get(this, slot), pars[i].ParameterType);
            slot += 1;
        }
        try
        {
            return _func.method.Invoke(target, args);
        }
        catch (Exception e)
        {
            Exception inner = e.InnerException;
            if (inner == null)
            {
                inner = e;
            }
            Console.WriteLine("[Pulse] " + member + " threw: " + inner.Message);
            return null;
        }
    }
}
