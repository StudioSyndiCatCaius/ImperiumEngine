using System.Numerics;
using Engine.Comps._1D.Systems;
using Engine.Core;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Comps._1D.Systems;


public class sys_Explore : C1_System
{
    [ImpVar][Config][Category("Camera")] public Vector3 camera_init_rotation;
    [ImpVar][Config][Category("Camera")] public bool camera_enable_rotate_H;
    [ImpVar][Config][Category("Camera")] public bool camera_enable_rotate_V;

    [ImpVar][Config][Category("States")] public TClass<C1_System> system_pause;


    public sys_Explore()
    {
        //blocked_system_tags = new("System.Explore");
    }

    public override void Input_Down(ImpPlayer player, TLabel ia, Vector3 axis, double dt)
    {
        base.Input_Down(player, ia, axis, dt);
        if (ia == "_Move")
        {
            if (player.pawn != null)
            {
                player.pawn.Phys_Move(axis);
            }
        }

        if (ia == "_Rotate")
        {
            player.control_rotation+=axis;
        }
    }
}
