using System.Reflection;
using ImperiumEngine.Assets;
using Raylib_cs;

namespace ImperiumEngine.Script.Node;

// Entry point for a [PulseOverride] on the script's parent type (UE "Event").
// Instanced per override, so it is never available on its own. The VM starts a chain here when
// the engine fires the matching lifecycle call; event args come out on the pins below exec.
public class SN_Event : ScriptNode
{
    public override Color GetNode_Color()
    {
        return new Color(180, 70, 70, 255);
    }

    public override void Slots_Build(List<TScriptSlot> slots)
    {
        slots.Add(new TScriptSlot { enable_right = true });

        TPulseFunc f = Pulse.FindOverride(context, member);
        if (f == null)
        {
            return;
        }
        ParameterInfo[] pars = f.pars;
        for (int i = 0; i < pars.Length; i++)
        {
            string n = pars[i].Name;
            if (string.IsNullOrEmpty(n))
            {
                n = "arg" + i;
            }
            slots.Add(new TScriptSlot
            {
                enable_right = true,
                type_right = pars[i].ParameterType,
                name_right = n,
            });
        }
    }

    public override void Compile_Check(TScriptProgram program, List<string> errors)
    {
        if (string.IsNullOrEmpty(member))
        {
            errors.Add("Event node has no member.");
            return;
        }
        // Events are matched by name when the engine fires them, so a stale target_type still
        // runs. Only warn when the script's own type has no such override at all.
        Type self_t = context;
        if (script != null)
        {
            self_t = script.ParentType_Get();
        }
        if (Pulse.FindOverride(self_t, member) == null && Pulse.FindOverride(context, member) == null)
        {
            errors.Add("Event '" + member + "' is not an override on " + self_t.Name + ".");
        }
    }

    // Slot 0 is exec; arg pins follow in parameter order.
    public override object Value_Get(ImpScriptVM vm, int slot)
    {
        object[] args = vm.Event_Args(this);
        if (args == null)
        {
            return null;
        }
        int i = slot - 1;
        if (i < 0 || i >= args.Length)
        {
            return null;
        }
        return args[i];
    }
}
