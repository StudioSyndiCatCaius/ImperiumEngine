//using raygui_cs;

using System.Numerics;
using ImperiumEngine.Assets;
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

    public static Imp3D view_target
    {
        get
        {
            if (ImpPlayer.players.Count > 0)
            {
                return ImpPlayer.players[0].target_view;
            }
            return null;
        }
        set
        {
            if (ImpPlayer.players.Count > 0)
            {
                ImpPlayer.players[0].target_view = value;
            }
        }
    }

    // Setup camera
    public Camera3D default_camera = new Camera3D() {
        Position = new Vector3(0, 2, 2),
        Target = Vector3.Zero,
        Up = new Vector3(0, 1, 0),
        FovY = 60
    };

    public Camera3D camera = new();
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
        ImpClassDefaults.LoadAll();
        
        // ---- Raylib
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | 
                              ConfigFlags.HighDpiWindow | 
                              ConfigFlags.ResizableWindow | 
                              ConfigFlags.MaximizedWindow );
        Raylib.InitWindow(1600, 900, "Imperium");
        
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        
        // ---- R3D
        // R3D has no cascades: the sun gets one ortho map, so map size and sun_shadow_range
        // together are the only lever on texel size. 4096 halves the texel for ~64 MB of depth.
        R3D.SetHint(Hint.ShadowDirSize,4096);

        // Internal R3D framebuffer. Use the HiDPI render size, not GetScreenWidth
        // (logical). MaximizeWindow in on_post_init happens after this, so the
        // first draw also Resolution_Sync's to the live window.
        int rw = Raylib.GetRenderWidth();
        int rh = Raylib.GetRenderHeight();
        if (rw < 1)
        {
            rw = 1;
        }
        if (rh < 1)
        {
            rh = 1;
        }
        R3D.Init(rw, rh);
        Imp3D.Resolution_Track(rw, rh);
        R3D.SetAspectMode(AspectMode.Expand);
        Imp3D.RefreshGraphics();
        
        // ---- IMP
        ImpPhys.Init();
        ImpPlayer.Init();
        
        on_post_init?.Invoke();
        Imp3D.Resolution_Sync(Raylib.GetRenderWidth(), Raylib.GetRenderHeight());
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
            Imp3D.Cache_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Input);
            foreach (var p in ImpPlayer.players)
                p.Update_Input(dt);

            // ----- UPDATE (layout) --------------------------------------------------------------------------
            Imp2D.Layout_Invalidate();
            Imp3D.Cache_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Update);
            ImpScene.current.Update(dt);

            if (view_target != null)
            {
                camera = R3D.CameraToRL(view_target.Camera_GetData());
            }
            else
            {
                camera = default_camera;
            }

            // ----- CURSOR (after layout so hit rects match drawn widgets) ------------------------------------
            Imp2D.Layout_Invalidate();
            Imp3D.Cache_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Cursor);
            foreach (var p in ImpPlayer.players)
                p.Update_Cursor(dt);

            // ----- DRAW ------------------------------------------------------------------------------------
            Imp2D.Layout_Invalidate();
            Imp3D.Cache_Invalidate();
            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Draw3D);
            // Standalone draws 3D to the window. Editor chrome (Scene_Editor) is also
            // is_running so its UI ticks, but its 3D lives in C2_Viewport3D — a window
            // pass here created a second sun and painted an empty sky behind the editor.
            bool window_3d = ImpScene.current.is_running;
            ImpComp host_root = ImpScene.current.root;
            if (window_3d && host_root != null && host_root.GetType().Name == "Scene_Editor")
            {
                window_3d = false;
            }
            if (window_3d)
            {
                Imp3D.Resolution_Sync(Raylib.GetRenderWidth(), Raylib.GetRenderHeight());
                ImpScene.current.ApplyRenderState();
                A_Material.draw_stamp++;
                if (view_target != null && view_target.Camera_IsValid())
                {
                    R3D.BeginEx(view_target.Camera_GetData());
                }
                else
                {
                    R3D.Begin(camera);
                }
                ImpScene.current.Draw(dt,0); //3D
                R3D.End();
            }

            ImpProfiler.Phase_Begin(ImpProfiler.EPhase.Draw2D);
            ImpScene.current.Draw(dt,1); //2D

            // ----- END ------------------------------------------------------------------------------------

            ImpProfiler.Phase_End();
            ImpProfiler.Frame_End();
            ImpProfiler.Draw();
            Raylib.EndDrawing();
        }

        on_shutdown?.Invoke();

        ImpPhys.Shutdown();
        R3D.Close();
        Raylib.ShowCursor();
        Raylib.CloseWindow();
    }
    
}