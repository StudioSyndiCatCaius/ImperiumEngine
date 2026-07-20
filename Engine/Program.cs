using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects._3D;
using ImperiumEngine.Objects.Assets;
using ImperiumEngine.Objects.Config;
using R3D_cs;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace ImperiumEngine;

public static class Program
{
    public static int Main(string[] args)
    {
        string projectDir = FindProjectDir();
        string engineContentDir = FindEngineContentDir();

        Console.WriteLine($"[Engine] Project dir:        {projectDir}");
        Console.WriteLine($"[Engine] Engine content dir: {engineContentDir}");

        ImpAsset.s_projectDir = projectDir;
        ImpAsset.s_engineContentDir = engineContentDir;

        var cfg = new CFG_Graphics();

        SetConfigFlags(ConfigFlags.MaximizedWindow);
        
        if (cfg.enable_window_resize)
            SetConfigFlags(ConfigFlags.ResizableWindow);

        InitWindow(cfg.resolution_width, cfg.resolution_height, "Imperium Engine");
        SetTargetFPS(cfg.enable_vsync ? 60 : 0);

        R3D.Init(GetScreenWidth(), GetScreenHeight());
        R3D.SetAntiAliasingMode(cfg.antialiasing_mode);

        var level = new A_Level();
        level.File_Load(Path.Combine(projectDir, "Content", "Levels", "test.ImpLvl"));

        var player = new ImpPlayer();
        ImpPlayer.s_active = player;

        foreach (var e in level.components)
        {
            e.Init();
            e.Begin();
        }

        bool isFullscreen = false;

        while (!WindowShouldClose())
        {
            double delta = GetFrameTime();
            
            
            // F11 — borderless fullscreen toggle
            if (cfg.enable_fullscreen_toggle && IsKeyPressed(KeyboardKey.F11))
            {
                isFullscreen = !isFullscreen;
                ToggleBorderlessWindowed();
            }

            if (IsWindowResized())
                R3D.SetResolution(GetScreenWidth(), GetScreenHeight());

            player.OnUpdate(delta);
            foreach (var e in level.components)
                e.Update(delta);

            BeginDrawing();
            ClearBackground(Color.Black);

            var camera = C3_Camera.active?.raycamera ?? player.camera;

            R3D.Begin(camera);
            foreach (var e in level.components)
                e.Draw(delta, camera, EDrawFlags.NONE);
            R3D.End();

            // Debug/gizmo pass — raylib 3D mode (required by e.g. R3D.DrawLightShape)
            BeginMode3D(camera);
            foreach (var e in level.components)
                e.Draw(delta, camera, EDrawFlags.DEBUG_PASS);
            EndMode3D();

            EndDrawing();
        }

        foreach (var e in level.components) e.End();
        R3D.Close();
        CloseWindow();

        return 0;
    }

    // Walk up from the exe directory looking for an .ImpGame file.
    // In dev, also checks Templates/ subdirectories so `dotnet run` from Engine/ finds DevTest.
    public static string FindProjectDir()
    {
        string dir = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            if (Directory.GetFiles(dir, "*.ImpGame").Length > 0)
                return dir;

            string templatesPath = Path.Combine(dir, "Templates");
            if (Directory.Exists(templatesPath))
            {
                foreach (string sub in Directory.GetDirectories(templatesPath))
                {
                    if (Directory.GetFiles(sub, "*.ImpGame").Length > 0)
                        return sub;
                }
            }

            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent == null || parent == dir) break;
            dir = parent;
        }

        Console.WriteLine("[Engine] WARNING: No .ImpGame found — using current directory");
        return Directory.GetCurrentDirectory();
    }

    // Walk up from the exe looking for Engine/Content (solution layout) or Content/3D (inside Engine/).
    public static string FindEngineContentDir()
    {
        string dir = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            string viaSolution = Path.Combine(dir, "Engine", "Content");
            if (Directory.Exists(viaSolution))
                return viaSolution;

            string viaDirect = Path.Combine(dir, "Content");
            if (Directory.Exists(Path.Combine(viaDirect, "3D")))
                return viaDirect;

            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent == null || parent == dir) break;
            dir = parent;
        }

        Console.WriteLine("[Engine] WARNING: Engine content directory not found");
        return "";
    }
}
