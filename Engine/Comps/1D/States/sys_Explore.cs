using System.Numerics;
using Engine.Comps._1D.Systems;
using Engine.Core;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Comps._1D.States;


public class sys_Explore : C1_State
{

    [ImpVar][Config][Category("States")] public TClass<C1_State> system_pause;
    
    public sys_Explore()
    {
        //blocked_system_tags = new("System.Explore");
    }

    public override void Input_Down(ImpPlayer player, TLabel ia, Vector3 axis, double dt)
    {
        base.Input_Down(player, ia, axis, dt);
        if (ia == "_move")
        {
            if (player.pawn != null)
            {
                player.pawn.Phys_MoveByRot(axis, player.control_rotation);
            }
        }

        if (ia == "_rotate")
        {
            player.control_rotation += axis;
        }
    }
}
