using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Engine.Assets;
using Engine.Comps._1D;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using R3D_cs;
using Raylib_cs;
using rlImGui_cs;
using Environment = System.Environment;

namespace Engine;

public struct TAppHooks
{
    public Action on_pre_init;
    public Action on_post_init;
    public Action on_shutdown;
    public Action on_draw_begin;
    public Action on_draw_end;
}

public enum EAppSceneState
{
    Idle, // idling on current scene. nothing happening 
    Loading, // transitioning to next scene
}

public class App
{
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static App app = null;
    public static ImpPlayer[] players=new []{new ImpPlayer()};

    // ========================================================================================
    // Mods
    // ========================================================================================
    
    public Dictionary<string,ImpMod> mods=new()
    {
        ["game"]=new() { type = EModuleType.Game },
        ["engine"]=new() { type = EModuleType.Engine, },
    };
    
    // ========================================================================================
    // Dialog
    // ========================================================================================
    public static ImpDialog dialog_current = null; // a dialog is a window that HOGS ALL INPUT until closed
    
    // ========================================================================================
    // Camera
    // ========================================================================================
    public static ImpViewport viewport_main = new();
    public static Camera2D camera_2d = new() { Zoom = 1 };
    
    public static Imp3D view_target=null;
    public static R3D_cs.Camera camera_view = new();
    public static Camera3D view_target_data = new();
    static bool imgui_enabled;

    public static string name = "Imperium";

    public static string version => "0.0.1";
    public static TVector2i window_size = TVector2i.p1440;
    public static TVector2i render_size = TVector2i.p1440;
    
    // ========================================================================================
    // Comp
    // ========================================================================================

    // public static Dictionary<ENotifyProcess, List<ImpComp>> comp_updates = new(); //NOT USING RIGHT NOW. look into implementatior DOD optimization
    
    // ========================================================================================
    // Scene
    // ========================================================================================
    
    public static A_Scene scene_current = null;
    public static A_Scene scene_next = null;
    public static A_Scene scene_persistent = null;
    
    public static List<A_Scene> scenes_global=new();
    public static EAppSceneState scene_state = EAppSceneState.Idle;
    public static Dictionary<TLabel,ImpComp> globalized_comps=new(); // comps manually given a global comp binding
    
    private static A_Scene scene_prev = null;
    
    // ========================================================================================
    // Save
    // ========================================================================================
    public static A_Save_Game save_game = new();
    public static A_Save_Global save_global = new();
    
    // ========================================================================================
    // Command Line Args
    // ========================================================================================
    private static Dictionary<string,string> args = new();
    
    public bool HasArg(string arg) { return args.ContainsKey(arg); }
    public static string getArg_String(string arg) { if(!args.ContainsKey(arg)) return ""; return args[arg]; }
    public static int getArg_Int(string arg) { int.TryParse(getArg_String(arg), out int val); return val; }
    public static bool getArg_Bool(string arg) { return getArg_String(arg) == "true"; }

    // ========================================================================================
    // Game
    // ========================================================================================
    
    public static string game_file = "";
    public static TTable game_data = new();
    public static Dictionary<TFile,ImpAsset> assets = new();
    public static Dictionary<TFile,ImpFile> files = new();
    
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // Class
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    [System.STAThread]
    public void Run(TAppHooks hooks=default, bool as_game=false, string force_game_path="")
    {
        app = this;

        // ========================================================================================-----------
        // ---- Parse Command Line Args
        
        string NormalizeArgName(string arg) { return arg.Trim().TrimStart('-', '/').ToLowerInvariant(); } 
        bool IsArgName(string value) { return value.StartsWith('-') || value.StartsWith('/'); }

        args.Clear();
        string[] launchArgs = Environment.GetCommandLineArgs();

        for (int i = 1; i < launchArgs.Length; i++)
        {
            string current = launchArgs[i];

            if (current.Contains('='))
            {
                string[] parts = current.Split('=', 2);
                string name = NormalizeArgName(parts[0]);
                string value = parts.Length > 1 ? parts[1] : "";

                args[name] = value;
                continue;
            }

            string key = NormalizeArgName(current);

            if (i + 1 < launchArgs.Length && !IsArgName(launchArgs[i + 1]))
            {
                args[key] = launchArgs[i + 1];
                i++;
            }
            else
            {
                args[key] = "true";
            }
        }
        
        // ========================================================================================-----------
        // --- Validate game
        if (as_game)
        {
            if (force_game_path != "")
            {
                game_file = force_game_path;
            }
            else if (args.ContainsKey("game"))
            {
                game_file = getArg_String("game");
            }
            else
            {
                game_file = GFile.GetFirstOfExt(GFile.GetExePath(), "ImpGame");
            }
            //if no valid .ImpGame file, exit.
            if (game_file == "" || !File.Exists(game_file))
            {
                Console.WriteLine("No valid ImpGame file found.");
                return;
            }

            game_data = TTable.FromTOML(GFile.LoadAs_String(game_file));
        }
        
        hooks.on_pre_init?.Invoke();
        Hooks.app_pre_init?.Invoke();
        GConfig.LoadGame();
        
        // ========================================================================================-----------
        // ---- Raylib
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | 
                              ConfigFlags.HighDpiWindow | 
                              ConfigFlags.ResizableWindow );
        Raylib.InitWindow(window_size.x, window_size.y, name);
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        if (as_game) BindRuntimeConsole();
        //Raylib.MaximizeWindow();
        
        // ========================================================================================-----------
        // ---- R3D
        R3D.SetHint(Hint.ShadowDirSize,4096);
        int rw = Raylib.GetRenderWidth();
        int rh = Raylib.GetRenderHeight();
        if (rw < 1) rw = 1;
        if (rh < 1) rh = 1;
        R3D.Init(rw, rh);
        R3D.SetAspectMode(AspectMode.Expand);
        Imp3D.ApplyRenderSettings();
        RefreshWindow(true);
        imgui_enabled = !as_game;
        if (imgui_enabled) rlImGui.Setup(true,true);
        
        // ---- IMP
        ImpPhysics.Init();
        //ImpPlayer.Init();
        
        hooks.on_post_init?.Invoke();
        RefreshWindow();

        ImpSandbox.current?.Init();
        Hooks.app_post_init?.Invoke();
        scene_persistent?.Begin();

        if (as_game)
        {
            string first_scene_path = getArg_String("scene");
            if (string.IsNullOrEmpty(first_scene_path))
                first_scene_path = game_data.get_String("starting_scene");
            if (!string.IsNullOrEmpty(first_scene_path))
            {
                A_Scene starting_scene = GAsset.Asset_Load<A_Scene>(first_scene_path);
                if (starting_scene != null)
                    scene_current = starting_scene.Clone() as A_Scene ?? starting_scene;
            }
        }
        // ========================================================================================----------------------------
        // LOOP
        // ========================================================================================----------------------------
        while (!Raylib.WindowShouldClose())
        {
            RefreshWindow();
            Imp3D.ApplyRenderSettings();
            double dt = Raylib.GetFrameTime();
            
            // -----------------------------------------------------
            // Scene Change
            // -----------------------------------------------------
            if (scene_prev != scene_current)
            {
                Hooks.scene_change_begin?.Invoke();
                scene_prev?.End();
                scene_state = EAppSceneState.Loading;
                scene_prev = scene_current;
            }
            else if (scene_state == EAppSceneState.Loading)
            {
                scene_state = EAppSceneState.Idle;
                scene_current?.Begin();
                Hooks.scene_change_end?.Invoke();
            }
            if(dialog_current!=null) { dialog_current.scene=scene_prev;}
            
            // -----------------------------------------------------
            // Player/Input update
            // -----------------------------------------------------
            ProcessUpdate(ENotifyProcess.CursorStack, dt);
            
            foreach (ImpPlayer player in players) player.Update(dt);

            ProcessUpdate(ENotifyProcess.Update, dt);
            ImpPhysics.Step((float)dt);
            
            // -----------------------------------------------------
            // Draw
            // -----------------------------------------------------
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            if (imgui_enabled) rlImGui.Begin();
            hooks.on_draw_begin?.Invoke();

            if (view_target != null && view_target.ViewTarget_Enabled())
            {
                camera_view = view_target.ViewTarget_GetData();
            }
            else
            {
                camera_view.Position = new Vector3(-3, 3, 3);
                camera_view.Fovy = 60.0f;
                camera_view.NearPlane = 0.1f;
                camera_view.FarPlane = 1000.0f;
                camera_view.CullMask = Layer.All;
                camera_view.Projection = Projection.Perspective;
                R3D.CameraLookAt(ref camera_view, Vector3.Zero, GMath.WORLD_UP);
            }
            view_target_data=R3D.CameraToRL(camera_view);
            
            // ---------- Draw 3D
            R3D.BeginEx(camera_view); 
            ProcessUpdate(ENotifyProcess.Draw3D, dt);
            // R3D_End is the actual 3D submit + post-process to the default framebuffer.
            // ImGui must render after that or the 3D pass covers the whole window.
            R3D.End();

            // R3D leaves depth test, culling, and its shader bound. 2D (C2_Image DrawTexturePro)
            // is culled or depth-rejected; button boxes still show via DrawRectanglePro fallback.
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableDepthTest();
            Rlgl.DisableBackfaceCulling();
            Rlgl.EnableShader(Rlgl.GetShaderIdDefault());
            Rlgl.SetBlendMode(Raylib_cs.BlendMode.Alpha);
            Rlgl.SetTexture(0);

            // ---------- Draw 2D
            Raylib.BeginMode2D(camera_2d);
            ProcessUpdate(ENotifyProcess.Draw2D, dt);
            Raylib.EndMode2D();

            hooks.on_draw_end?.Invoke();
            if (imgui_enabled) rlImGui.End();
            Raylib.EndDrawing();
        }
        
        // -----------------------------------------------------
        // Shutdown
        // -----------------------------------------------------
        
        scene_current?.End();
        hooks.on_shutdown?.Invoke();
        ImpSandbox.current?.Shutdown();
        ImpPhysics.Shutdown();
        
        R3D.Close();
        if (imgui_enabled) rlImGui.Shutdown();
        Raylib.ShowCursor();
        Raylib.CloseWindow();
    }
    
    private void ProcessUpdate(ENotifyProcess notify, double dt)
    {
        if(notify==ENotifyProcess.CursorStack) Imp2D.cursortrace_stack.Clear();
        scene_persistent?.ProcessNotify(notify, dt);
        foreach (A_Scene scene in scenes_global) scene.ProcessNotify(notify, dt);
        scene_current?.ProcessNotify(notify, dt);
        dialog_current?.ProcessNotify(notify, dt);
    }

    private static void RefreshWindow(bool force = false)
    {
        int rw = Raylib.GetRenderWidth();
        int rh = Raylib.GetRenderHeight();
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        if (rw < 1) rw = 1;
        if (rh < 1) rh = 1;
        if (sw < 1) sw = 1;
        if (sh < 1) sh = 1;

        if (!force && rw == render_size.x && rh == render_size.y
            && (int)viewport_main.size.X == sw && (int)viewport_main.size.Y == sh)
        {
            return;
        }

        render_size.x = rw;
        render_size.y = rh;
        // 2D Format/draw is in screen pixels; HighDPI framebuffer is only for R3D.
        viewport_main.size = new Vector2(sw, sh);

        // Use the actual R3D resize/refresh method exposed by your binding if this name differs.
        R3D.SetResolution(render_size.x, render_size.y);
        R3D.SetAspectMode(AspectMode.Expand);
    }

    static void BindRuntimeConsole()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (GetConsoleWindow() == IntPtr.Zero) AllocConsole();
        SetConsoleTitleW("Imperium Game");
        try
        {
            Encoding enc = Console.OutputEncoding;
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), enc) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), enc) { AutoFlush = true });
        }
        catch { }
    }

    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll")] static extern bool AllocConsole();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetConsoleTitleW(string title);
}