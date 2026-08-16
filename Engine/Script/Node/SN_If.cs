using ImperiumEngine.Assets;
using Raylib_cs;

namespace ImperiumEngine.Script.Node;

// Splits exec on a bool. Not tied to any member, so it can be added anywhere.
public class SN_If : ScriptNode
{
    [ImpVar] public bool condition;

    public SN_If()
    {
        is_available = true;
    }

    public override string GetNode_Title()
    {
        return "If";
    }

    public override Color GetNode_Color()
    {
        return new Color(90, 90, 100, 255);
    }

    public override void Slots_Build(List<TScriptSlot> slots)
    {
        slots.Add(new TScriptSlot { enable_left = true, enable_right = true, name_right = "True" });
        slots.Add(new TScriptSlot
        {
            enable_left = true,
            type_left = typeof(bool),
            name_left = nameof(condition),
            enable_right = true,
            name_right = "False",
        });
    }

    // True is the exec out on row 0, False on row 1.
    public override int Exec_Run(ImpScriptVM vm)
    {
        object v = vm.Input_Get(this, 1);
        condition = false;
        if (v is bool b)
        {
            condition = b;
        }
        if (condition)
        {
            return 0;
        }
        return 1;
    }
}
