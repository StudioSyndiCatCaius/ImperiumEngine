using ImperiumEngine.Assets;
using Raylib_cs;

namespace ImperiumEngine.Script.Node;

// Latent wait. Async nodes are always available.
public class SN_Delay : ScriptNodeAsync
{
    [ImpVar] public float time;

    public override string GetNode_Title()
    {
        return "Delay";
    }

    public override Color GetNode_Color()
    {
        return new Color(90, 90, 100, 255);
    }

    public override void Slots_Build(List<TScriptSlot> slots)
    {
        slots.Add(new TScriptSlot { enable_left = true, enable_right = true, name_right = "Completed" });
        slots.Add(new TScriptSlot { enable_left = true, type_left = typeof(float), name_left = nameof(time) });
    }

    // Hands the chain to the VM's timer list and resumes out of Completed when it runs down.
    public override int Exec_Run(ImpScriptVM vm)
    {
        object v = ImpScriptVM.Value_As(vm.Input_Get(this, 1), typeof(float));
        time = 0f;
        if (v is float f)
        {
            time = f;
        }
        vm.Latent_Schedule(this, 0, time);
        return ImpScriptVM.EXEC_LATENT;
    }
}
