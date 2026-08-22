using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// Viewport for a running game. Used for Play-in-Editor; may later host local split-screen.
[ImpClass(Hidden = true)]
public class C2_GameView : C2_Box
{
    // The session this widget is showing (PIE). Owning editor session is ImpComp.game_owner.
    public ImpGame view_game;
    public ImpPlayer player;

    public C2_Viewport3D viewport3D = new()
    {
        layout = TLayout2.FULL,
        cursor_filter = ECursorFilter.Pass,
    };

    public C2_Viewport2D viewport2D = new()
    {
        layout = TLayout2.FULL,
        cursor_filter = ECursorFilter.Pass,
        clear_background = false,
        draw_canvas = false,
    };

    ImpGame _draw_prev;

    public C2_GameView()
    {
        name = "Game";
        layout = TLayout2.FULL;
        clip_children = false;
        is_visible = true;
        cursor_filter = ECursorFilter.Hit;
        style = UI_Box.BkgDark;
        Child_Add(viewport3D);
        Child_Add(viewport2D);
    }

    public void Bind(ImpGame g, Camera3D cam3)
    {
        view_game = g;
        ImpScene s = g?.scene;
        viewport3D.view_scene = s;
        viewport2D.view_scene = s;
        viewport3D.camera = cam3;
        Camera_Sync();
        if (ImpPlayer.players.Count > 0)
        {
            player = ImpPlayer.players[0];
        }
        // Play starts focused on the game. Every player, not just player 0: only player 0 has a
        // cursor, so gamepad players could never click in and would drive editor comps all session.
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            ImpPlayer.players[i].TargetGame_Set(g);
        }
    }

    public void Unbind()
    {
        // Hand input back before the session pointer goes away.
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            ImpPlayer p = ImpPlayer.players[i];
            if (view_game != null && p.target_game == view_game)
            {
                p.TargetGame_Set(null);
            }
        }
        view_game = null;
        player = null;
        viewport3D.view_scene = null;
        viewport2D.view_scene = null;
        viewport3D.view_camera = null;
    }

    void Camera_Sync()
    {
        ImpScene s = view_game?.scene;
        if (s != null && s.starting_camera != null && s.starting_camera.Camera_IsValid())
        {
            viewport3D.view_camera = s.starting_camera;
            viewport3D.camera = R3D.CameraToRL(s.starting_camera.Camera_GetData());
            return;
        }
        viewport3D.view_camera = null;
    }

    // Click in / click out. ImpPlayer sets target_focus to the deepest Hit comp under the cursor,
    // and fires Begin / End on the pair when it changes.
    public override void _Notify_AsFocusTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsFocusTarget(player, notify, dt);
        if (player == null || view_game == null)
        {
            return;
        }
        if (notify == ENotifyGeneric.Begin)
        {
            player.TargetGame_Set(view_game);
            return;
        }
        if (notify == ENotifyGeneric.End && player.target_game == view_game)
        {
            player.TargetGame_Set(null);
        }
    }

    // Focus only fires Begin when target_focus *changes*. Once a hotkey can force input back to
    // the editor while this view is still focused, clicking inside it would raise nothing — so
    // re-claim on the click itself. Idempotent.
    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (player == null || view_game == null)
        {
            return;
        }
        if (evnt == ECursorEvent.Select_A || evnt == ECursorEvent.Select_B)
        {
            player.TargetGame_Set(view_game);
        }
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        ImpScene s = view_game?.scene;
        if (s == null)
        {
            return;
        }

        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X > 1f && dim.size.Y > 1f)
        {
            viewport2D.camera.position = dim.size * 0.5f;
            viewport2D.camera.zoom = 1f;
        }

        ImpGame prev = ImpGame.Bind(view_game);
        s.Update(dt);
        Camera_Sync();
        ImpGame.Bind(prev);
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        _draw_prev = ImpGame.current;
        if (view_game != null)
        {
            ImpGame.Bind(view_game);
        }
        base.OnDraw2D(dt, flags);
    }

    public override void OnDraw2DForeground(double dt, EDrawFlags flags)
    {
        base.OnDraw2DForeground(dt, flags);
        ImpGame.Bind(_draw_prev);
        _draw_prev = null;
    }
}
