using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Engine.Assets;
using Engine.Core;
using Engine.Sandbox;
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
    // ==============================================================================================================
    // STATIC
    // ==============================================================================================================
    public static App app = null;
    public static ImpPlayer[] players=new []{new ImpPlayer()};

    public Dictionary<string,ImpMod> mods=new()
    {
        ["game"]=new() { type = EModuleType.Game },
        ["engine"]=new() { type = EModuleType.Engine, },
    };
    
    
    // -----------------------------------------------------------------------
    // Camera
    // -----------------------------------------------------------------------
    public static ImpViewport viewport_main = new();
    public static Camera2D camera_2d = new() { Zoom = 1 };
    
    public static Imp3D view_target=null;
    public static R3D_cs.Camera camera_view = new();
    public static Camera3D view_target_data = new();

    public static string name = "Imperium";

    public static string version => "0.0.1";
    public static TVector2i window_size = TVector2i.p1440;
    public static TVector2i render_size = TVector2i.p1440;
    
    // -----------------------------------------------------------------------
    // Scene
    // -----------------------------------------------------------------------
    
    public static A_Scene scene_current = null;
    public static A_Scene scene_next = null;
    public static List<A_Scene> scenes_global=new();
    public static EAppSceneState scene_state = EAppSceneState.Idle;
    
    private static A_Scene scene_prev = null;

    
    // -----------------------------------------------------------------------
    // Command Line Args
    // -----------------------------------------------------------------------
    private static Dictionary<string,string> args = new();
    
    public bool HasArg(string arg) { return args.ContainsKey(arg); }
    public static string getArg_String(string arg) { if(!args.ContainsKey(arg)) return ""; return args[arg]; }
    public static int getArg_Int(string arg) { int.TryParse(getArg_String(arg), out int val); return val; }
    public static bool getArg_Bool(string arg) { return getArg_String(arg) == "true"; }

    // -----------------------------------------------------------------------
    // Game
    // -----------------------------------------------------------------------
    
    public static string game_file = "";
    public static TTable game_data = new();
    public static Dictionary<TFile,ImpAsset> assets = new();
    public static Dictionary<TFile,ImpFile> files = new();
    
    // ==============================================================================================================
    // Class
    // ==============================================================================================================
    
    [System.STAThread]
    public void Run(TAppHooks hooks=default, bool as_game=false, string force_game_path="")
    {
        app = this;
        Imp.A = this;
        
        // ----------------------------------------------------------------------------------
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
        
        // ----------------------------------------------------------------------------------
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
                game_file = Imp.File_GetFirstOfExt(Imp.GetExePath(), "ImpGame");
            }
            //if no valid .ImpGame file, exit.
            if (game_file == "" || !File.Exists(game_file))
            {
                Console.WriteLine("No valid ImpGame file found.");
                return;
            }

            game_data = TTable.FromTOML(Imp.File_LoadAs_String(game_file));
        }
        
        hooks.on_pre_init?.Invoke();
        Hooks.app_pre_init?.Invoke();
        
        // ----------------------------------------------------------------------------------
        // ---- Raylib
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | 
                              ConfigFlags.HighDpiWindow | 
                              ConfigFlags.ResizableWindow );
        Raylib.InitWindow(window_size.x, window_size.y, name);
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        if (as_game) BindRuntimeConsole();
        //Raylib.MaximizeWindow();
        
        // ----------------------------------------------------------------------------------
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
        rlImGui.Setup(true,true);
        
        // ---- IMP
        //ImpPhys.Init();
        //ImpPlayer.Init();
        
        hooks.on_post_init?.Invoke();
        RefreshWindow();

        ImpSandbox.current = new Sandbox_Lua();
        ImpSandbox.current.Init();
        if (as_game) ImpSandbox.LoadGlobals();
        Hooks.app_post_init?.Invoke();

        if (as_game)
        {
            string first_scene_path = getArg_String("scene");
            if (string.IsNullOrEmpty(first_scene_path))
                first_scene_path = game_data.get_String("starting_scene");
            if (!string.IsNullOrEmpty(first_scene_path))
            {
                A_Scene starting_scene = Imp.Asset_Load<A_Scene>(first_scene_path);
                if (starting_scene != null)
                {
                    starting_scene.is_runtime = true;
                    scene_current = starting_scene;
                }
            }
        }
        
        while (!Raylib.WindowShouldClose())
        {
            RefreshWindow();
            Imp3D.ApplyRenderSettings();
            double dt = Raylib.GetFrameTime();

            foreach (A_Scene scene in scenes_global) scene.Update(dt);

            // -------- SCENE
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
            scene_prev?.Update(dt);

            foreach (ImpPlayer player in players) player.Update(dt);

            // -------- DRAW
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            rlImGui.Begin();
            hooks.on_draw_begin?.Invoke();

            if (view_target != null && view_target.ViewTarget_Enabled())
            {
                camera_view = view_target.ViewTarget_GetData();
            }
            else
            {
                camera_view = new R3D_cs.Camera
                {
                    Position = new Vector3(-3, 3, 3),
                    Fovy = 60.0f,
                    NearPlane = 0.1f,
                    FarPlane = 1000.0f,
                    CullMask = Layer.All,
                    Projection = Projection.Perspective,
                };
                R3D.CameraLookAt(ref camera_view, Vector3.Zero, Imp.WORLD_UP);
            }
            view_target_data=R3D.CameraToRL(camera_view);
            R3D.BeginEx(camera_view);

            foreach (A_Scene scene in scenes_global) scene.Draw(dt, EDrawFlags.No2D);
            scene_prev?.Draw(dt, EDrawFlags.No2D);

            // R3D_End is the actual 3D submit + post-process to the default framebuffer.
            // ImGui must render after that or the 3D pass covers the whole window.
            R3D.End();

            Raylib.BeginMode2D(camera_2d);
            foreach (A_Scene scene in scenes_global) scene.Draw(dt, EDrawFlags.No3D);
            scene_prev?.Draw(dt, EDrawFlags.No3D);
            Raylib.EndMode2D();

            hooks.on_draw_end?.Invoke();
            rlImGui.End();
            Raylib.EndDrawing();
        }
        hooks.on_shutdown?.Invoke();
        ImpSandbox.current?.Shutdown();
        
        R3D.Close();
        rlImGui.Shutdown();
        Raylib.ShowCursor();
        Raylib.CloseWindow();
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
        // 2D layout/draw is in screen pixels; HighDPI framebuffer is only for R3D.
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