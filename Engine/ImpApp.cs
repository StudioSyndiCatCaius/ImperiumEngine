using System.Data;
using System.Numerics;
using ImperiumEngine.Main;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine;

//coremost class for am Imperium engine app. Used in the game, editor, and launcher

public class ImpApp
{
    public static ImpApp app;
    public static string name="ImpApp";

    //default framing a new C2_SceneView starts from, until something points it elsewhere
    public static Camera3D camera=new Camera3D(){
        Position = new Vector3(0, 2, 2),
        Target = Vector3.Zero,
        Up = new Vector3(0, 1, 0),
        FovY = 60
    };
    
    public void Run(Action? pre_loop=null)
    {
        app = this;
        Raylib.InitWindow(1280, 720, name);
        R3D.Init(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

        
        //local player 0: every Key_/Action_ query is routed through a player
        ImpPlayer.Create();

        pre_loop?.Invoke();

        while (!Raylib.WindowShouldClose())
        {
            double dt = Raylib.GetFrameTime();

            // INPUT ----------------------------------
            // Sampled once, up front, so every query for the rest of the frame agrees.
            ImpPlayer.Input_Poll(dt);
            ImpUI.Frame_Begin(dt);

            // UPDATE ----------------------------------
            ImpScene.current.Update(dt);
            ImpScene.global.Update(dt);

            // BEGIN ----------------------------------
            Raylib.BeginDrawing();
            Raylib.ClearBackground(ImpUITheme.game_theme.col_background);

            // UI ------------------------------------
            // Three ordered passes: layout resolves every comp's screen rect, input
            // hit-tests those rects, then draw paints using them. Input sits between so
            // a comp can render its pressed state on the same frame it was pressed.
            //
            // 3D goes out during the draw pass too: a C2_SceneView opens its own R3D
            // session over its rect, so scenes reach the screen through a viewport comp
            // in a game exactly as they do in the editor.
            ImpScene.current.Layout(ImpUI.screen);
            ImpScene.global.Layout(ImpUI.screen);

            ImpUI.Input_Process(ImpScene.current.root, ImpScene.global.root);

            ImpScene.current.Draw(dt, EDrawFlags.None);
            ImpScene.global.Draw(dt, EDrawFlags.None);

            ImpUI.Frame_End();

            // END ----------------------------------
            Raylib.EndDrawing();
        }
        R3D.Close();
        Raylib.CloseWindow();
    }
}