using System.Numerics;
using Engine.Comps._1D.Systems;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Comps._1D.Systems;


public class sys_Explore : C1_GameSystem
{

    [ImpVar][Config][Category("States")] public TClass<C1_GameSystem> system_pause;
    
    public sys_Explore()
    {
        //blocked_system_tags = new("System.Explore");
    }

    public override void OnBegin()
    {
        base.OnBegin();
        ImpPlayer.Get().cursor_data.is_visible = false;
    }
    public override void OnEnd()
    {
        base.OnEnd();
        ImpPlayer.Get().cursor_data.is_visible = true;
    }

    public override void Input_Down(ImpPlayer player, TLabel ia, Vector3 axis, double dt)
    {
        base.Input_Down(player, ia, axis, dt);
        if (ia == "_move")
        {
            Console.WriteLine("move axis: "+axis);
            //Console.WriteLine("move");
            if (player.pawn != null)
            {
                Vector3 floor_axis=player.control_rotation*GMath.NO_UP;
                player.pawn.Phys_MoveByRot(axis, floor_axis);
            }
        }

        if (ia == "_rotate")
        {
            player.control_rotation += axis;
        }
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        //Console.WriteLine("update");
    }
}
