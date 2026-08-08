using System.Numerics;
using ImperiumEngine.Main;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

public class C3_PlayerStart : ImpComp3D
{
    [ImpVar] public TRef<ImpScene> linked_scene;
    [ImpVar] public int player_index = 0;

    public override void OnDraw(double dt, EDrawFlags flags)
    {
        base.OnDraw(dt, flags);
        if (flags.HasFlag(EDrawFlags.DebugDraw))
        {
            TTransform3 _t = Transform_Get(true);
            Imp3D.Draw_Arrow(_t.position,_t.rotation,5,1,Color.DarkGreen);
            Imp3D.Draw_Capsule(_t.position,45,90,1,Color.Green);
        }
    }
}