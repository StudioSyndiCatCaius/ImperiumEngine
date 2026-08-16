using System.Numerics;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D.States;

public class State_Exploration : C1_GameState
{
    [ImpVar][Category("Camera")] public Vector3 camera_init_rotation;
    [ImpVar][Category("Camera")] public bool camera_enable_rotate_H;
    [ImpVar][Category("Camera")] public bool camera_enable_rotate_V;
    
    [ImpVar][Category("States")] public TClass<C1_GameState> state_pause;
    
}