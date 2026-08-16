//using raygui_cs;

using System.Numerics;
using ImperiumEngine.Comps;
using ImperiumEngine.Enums;
using R3D_cs;
using raygui_cs;
using Raylib_cs;

namespace ImperiumEngine;

public class ImpApp
{
    // #################################################################################
    // Static
    // #################################################################################
    
    //if true, the game autostarts in borderless window mode by default.
    [Category("Window")][ImpVar][Config] public static bool start_fullscreen = false;
    [Category("Window")][ImpVar][Config] public static Vector2 window_size = new(1280, 720);
    
    
    public static ImpApp app;
    
    
// Setup camera
    public Camera3D camera = new Camera3D() {
        Position = new Vector3(0, 2, 2),
        Target = Vector3.Zero,
        Up = new Vector3(0, 1, 0),
        FovY = 60
    };
    // #################################################################################
    // Class
    // #################################################################################
    
    [System.STAThread]
    public void Run(Action on_pre_init=null,Action on_post_init=null,Action on_shutdown=null)
    {
        // init
        app = this;
        
        on_pre_init?.Invoke();
        ImpConfig.LoadAll();
        
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.HighDpiWindow);
        Raylib.InitWindow(1280, 720, "Imperium");
        
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        R3D.Init(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        R3D.SetAspectMode(AspectMode.Expand);
        ImpPlayer.Init();
        
        on_post_init?.Invoke();
        ImpGame.EnsureHost();
        
        while (!Raylib.WindowShouldClose())
        {
            // ----- BEGIN ------------------------------------------------------------------------------------
            double dt = Raylib.GetFrameTime();
            ImpProfiler.Frame_Begin();
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);

            // ----- INPUT (keys first) -----------------------------------------------------------------------
            // Each phase starts on a fresh layout epoch: the previous phase may have moved
            // things around through paths the setters cannot see (direct size/transform writes).
            Imp2D.Layout_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Input);
            foreach (var p in ImpPlayer.players)
                p.Update_Input(dt);

            // ----- UPDATE (layout) --------------------------------------------------------------------------
            Imp2D.Layout_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Update);
            ImpScene.current.Update(dt);

            // ----- CURSOR (after layout so hit rects match drawn widgets) ------------------------------------
            Imp2D.Layout_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Cursor);
            foreach (var p in ImpPlayer.players)
                p.Update_Cursor(dt);

            // ----- DRAW ------------------------------------------------------------------------------------
            Imp2D.Layout_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Draw3D);
            R3D.Begin(camera);
            ImpScene.current.Draw(dt,0); //3D
            R3D.End();

            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Draw2D);
            ImpScene.current.Draw(dt,1); //2D

            // ----- END ------------------------------------------------------------------------------------

            ImpProfiler.Phase_End();
            ImpProfiler.Frame_End();
            ImpProfiler.Draw();
            Raylib.EndDrawing();
        }

        on_shutdown?.Invoke();

        R3D.Close();
        Raylib.ShowCursor();
        Raylib.CloseWindow();
    }
    
}