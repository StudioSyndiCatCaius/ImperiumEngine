using System.Numerics;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D.States;

public class sys_Explore : C1_GameSystem
{
    [ImpVar][Category("Camera")] public Vector3 camera_init_rotation;
    [ImpVar][Category("Camera")] public bool camera_enable_rotate_H;
    [ImpVar][Category("Camera")] public bool camera_enable_rotate_V;
    
    //system activated on Pause Input
    [ImpVar][Category("States")] public TClass<C1_GameSystem> system_pause;

    public override void OnBegin()
    {
        base.OnBegin();
        
    }

    public override void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        if (iaction == "_Pause")
        {
            
        }
    }
}