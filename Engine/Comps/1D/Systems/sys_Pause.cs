using System.Numerics;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D.Systems;

// the pause menu
public class sys_Pause : C1_GameSystem
{
    public sys_Pause()
    {
        blocked_systems=new TTagSet("System.Explore");
    }
    
    public override void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        if (iaction == "_Pause")
        {
            Destroy();
        }
    }
}