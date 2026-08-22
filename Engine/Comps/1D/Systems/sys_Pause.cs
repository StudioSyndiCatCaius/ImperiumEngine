using System.Numerics;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D.Systems;

public class sys_Pause : C1_GameSystem
{
    public sys_Pause()
    {
        system_tags = new TTagSet("System.Pause");
        blocked_systems = new TTagSet("System.Explore");
    }

    public override void OnBegin()
    {
        base.OnBegin();
        if (ImpPlayer.players.Count > 0)
        {
            Input_SetOwnerActive(0, true);
        }
    }

    public override void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        base.Input_Pressed(player, iaction, axis);
        if (iaction == "_Pause")
        {
            Destroy();
        }
    }
}
