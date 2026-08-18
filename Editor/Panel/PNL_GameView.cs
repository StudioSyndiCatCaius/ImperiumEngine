using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Panel;

// Play-in-Editor tab. Hosts C2_GameView; the session ticks even while this tab is hidden.
public class PNL_GameView : EdPanel
{
    public C2_GameView view_game = new();

    public bool IsPlaying
    {
        get { return view_game.view_game != null; }
    }

    public PNL_GameView()
    {
        name = "Game";
        layout = TLayout2.FULL;
        cursor_filter = ECursorFilter.Pass;
        view_game.name = "Game";
        view_game.layout = TLayout2.FULL;
        view_game.is_visible = true;
        Child_Add(view_game);
    }

    public void Play(ImpGame game, Camera3D cam3)
    {
        view_game.Bind(game, cam3);
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].target_focus = view_game;
        }
    }

    public void Stop()
    {
        view_game.Unbind();
    }
}
