using System.Numerics;
using ImperiumEngine.Comps._1D.Systems;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._1D.Systems;


public class sys_Explore : C1_GameSystem
{
    [ImpVar][Config][Category("Camera")] public Vector3 camera_init_rotation;
    [ImpVar][Config][Category("Camera")] public bool camera_enable_rotate_H;
    [ImpVar][Config][Category("Camera")] public bool camera_enable_rotate_V;

    [ImpVar][Config][Category("States")] public TClass<C1_GameSystem> system_pause;

    public sys_Explore()
    {
        system_tags = new TTagSet("System.Explore");
        system_pause = new TClass<C1_GameSystem>(typeof(sys_Pause));
    }

    public override void OnBegin()
    {
        base.OnBegin();
        if (ImpPlayer.players.Count > 0)
        {
            Input_SetOwnerActive(0, true);
            ImpPlayer.players[0].cursor.is_hidden = true;
            Raylib.DisableCursor();
        }
    }

    public override void OnEnd()
    {
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].cursor.is_hidden = false;
            Raylib.EnableCursor();
        }
        base.OnEnd();
    }

    protected override void OnDestroy()
    {
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].cursor.is_hidden = false;
            Raylib.EnableCursor();
        }
        base.OnDestroy();
    }

    public override void Input_Update(ImpPlayer player, TLabel iaction, double dt, Vector3 axis)
    {
        base.Input_Update(player, iaction, dt, axis);
        if (player.target_view != null)
        {
            player.target_view.Input_Update(player, iaction, dt, axis);
        }
    }

    public override void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        base.Input_Pressed(player, iaction, axis);
        if (iaction == "_Pause")
        {
            C1_GameSystem.Activate(system_pause);
            return;
        }
        if (player.target_view != null)
        {
            player.target_view.Input_Pressed(player, iaction, axis);
        }
    }

    public override void Input_Released(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        base.Input_Released(player, iaction, axis);
        if (player.target_view != null)
        {
            player.target_view.Input_Released(player, iaction, axis);
        }
    }
}
