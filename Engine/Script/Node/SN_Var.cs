using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using Raylib_cs;

namespace ImperiumEngine.Script.Node;

public enum EScriptVarSize
{
    Single, List, Dictionary
}

// Get / set of an [ImpVar] whose EImpVarEdit is ReadOnly or ReadWrite, or of a script-local var.
// Instanced per variable, so neither is available on its own. Set only exists for ReadWrite.
public abstract class SN_Var : ScriptNode
{
    EScriptVarSize var_size;

    protected TPulseVar var_info; // null for script-local vars, which live on the VM instead

    public override void Bind(TPulseNode node, Type ctx)
    {
        base.Bind(node, ctx);
        var_info = Pulse.FindVar(context, member);
    }

    public override Color GetNode_Color()
    {
        return new Color(180, 140, 50, 255);
    }

    public override EGraphNodeChrome GetNode_Chrome()
    {
        return EGraphNodeChrome.Var;
    }

    public Type Var_Type()
    {
        if (var_info != null)
        {
            return var_info.type;
        }
        if (script != null)
        {
            Type local = script.Var_TypeOf(member);
            if (local != null)
            {
                return local;
            }
        }
        return typeof(object);
    }

    // Same stale-target_type rebind as SN_Func: a var node authored against the old scene context
    // still points at that type.
    protected void Var_Rebind(TScriptProgram program, int target_slot, List<string> errors)
    {
        if (string.IsNullOrEmpty(member))
        {
            errors.Add("Var node has no member.");
            return;
        }
        if (script == null)
        {
            return;
        }
        Type self_t = script.ParentType_Get();
        if (var_info != null && var_info.owner != null && var_info.owner.IsAssignableFrom(self_t))
        {
            return;
        }
        if (program != null && program.Input_IsWired(guid, target_slot))
        {
            return;
        }
        TPulseVar alt = Pulse.FindVar(self_t, member);
        if (alt != null)
        {
            var_info = alt;
            context = self_t;
            return;
        }
        if (script.Var_TypeOf(member) == null)
        {
            errors.Add("Var '" + member + "' not found on " + self_t.Name + ".");
        }
    }

    protected object Var_Read(ImpScriptVM vm, object target)
    {
        if (var_info == null)
        {
            return vm.Local_Get(member);
        }
        FieldInfo f = var_info.member as FieldInfo;
        if (f != null)
        {
            return f.GetValue(target);
        }
        PropertyInfo p = var_info.member as PropertyInfo;
        if (p != null)
        {
            return p.GetValue(target);
        }
        return null;
    }
}

public class SN_VarSet : SN_Var
{
    public override string GetNode_Title()
    {
        return "Set " + member;
    }

    public override void Slots_Build(List<TScriptSlot> slots)
    {
        Type vt = Var_Type();
        slots.Add(new TScriptSlot { enable_left = true, enable_right = true });
        slots.Add(new TScriptSlot { enable_left = true, type_left = context, name_left = "Target" });
        slots.Add(new TScriptSlot { enable_left = true, type_left = vt, name_left = member });
    }

    public override void Compile_Check(TScriptProgram program, List<string> errors)
    {
        Var_Rebind(program, 1, errors);
        if (var_info != null && !var_info.can_set)
        {
            errors.Add("Var '" + member + "' is read only.");
        }
    }

    public override int Exec_Run(ImpScriptVM vm)
    {
        object value = ImpScriptVM.Value_As(vm.Input_Get(this, 2), Var_Type());
        if (var_info == null)
        {
            vm.Local_Set(member, value);
            return 0;
        }
        object target = vm.Target_Get(this, 1);
        if (target == null)
        {
            Console.WriteLine("[Pulse] Set " + member + ": no target.");
            return 0;
        }
        FieldInfo f = var_info.member as FieldInfo;
        if (f != null)
        {
            f.SetValue(target, value);
            return 0;
        }
        PropertyInfo p = var_info.member as PropertyInfo;
        if (p != null && p.CanWrite)
        {
            p.SetValue(target, value);
        }
        return 0;
    }
}

public class SN_VarGet : SN_Var
{
    private bool is_validate; // for bools & objects. expands as Exec with a true/false output pins

    public override string GetNode_Title()
    {
        return "Get " + member;
    }

    public override void Slots_Build(List<TScriptSlot> slots)
    {
        slots.Add(new TScriptSlot
        {
            enable_left = true,
            type_left = context,
            name_left = "Target",
            enable_right = true,
            type_right = Var_Type(),
            name_right = member,
        });
    }

    public override void Compile_Check(TScriptProgram program, List<string> errors)
    {
        Var_Rebind(program, 0, errors);
    }

    public override object Value_Get(ImpScriptVM vm, int slot)
    {
        object target = vm.Target_Get(this, 0);
        return Var_Read(vm, target);
    }
}
