using Editor.Windows;
using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;
using R3D_cs;
using Raylib_cs;
using rlImGui_cs;
using static Raylib_cs.Raylib;

namespace Editor;

public static class Program
{
    public static WND_LevelEdit window_main = null!; //the main window (which is the level editor). Closing this will close the program.
    public static List<EditorWindow> windows_open = new(); //all the windows that are open. Closing these will NOT close the program (asides from the main window)

    static bool _exit_requested;

    public static int Main()
    {
        string projectDir = ImperiumEngine.Program.FindProjectDir();
        string engineContentDir = ImperiumEngine.Program.FindEngineContentDir();

        Console.WriteLine($"[Editor] Project dir:        {projectDir}");
        Console.WriteLine($"[Editor] Engine content dir: {engineContentDir}");

        ImpAsset.s_projectDir = projectDir;
        ImpAsset.s_engineContentDir = engineContentDir;

        SetConfigFlags(ConfigFlags.ResizableWindow);
        InitWindow(1600, 900, "Imperium Editor");
        SetTargetFPS(60);

        R3D.Init(GetScreenWidth(), GetScreenHeight());
        rlImGui.Setup(darkTheme: true, enableDocking: true);
        ApplyEditorTheme();

        var level = new A_Level();
        level.Load(Path.Combine(projectDir, "Content", "Levels", "test.ImpLvl"));

        // The editor previews the level live for now: init + begin so environment/
        // lights apply, and tick OnUpdate so R3D light state stays in sync.
        foreach (var c in level.components)
        {
            c.OnInit();
            c.OnBegin();
        }

        window_main = new WND_LevelEdit();
        window_main.SetLevel(level);
        window_main.Open();
        windows_open.Add(window_main);

        while (!WindowShouldClose() && !_exit_requested)
        {
            double delta = GetFrameTime();

            foreach (var c in level.components)
                if (c.is_active) c.OnUpdate(delta);

            foreach (var w in windows_open)
                w.Update(delta);

            BeginDrawing();
            ClearBackground(new Color(25, 25, 28, 255));

            // 3D world renders into the viewport texture before the ImGui pass
            window_main.pnl_world.RenderWorld(delta);

            rlImGui.Begin();

            DrawMainMenuBar();
            uint dock_id = ImGui.DockSpaceOverViewport();

            foreach (var w in windows_open.ToArray())
                w.DrawWindow(delta, dock_id);
            windows_open.RemoveAll(w => !w.is_open);

            rlImGui.End();
            EndDrawing();
        }

        foreach (var c in level.components) c.OnEnd();

        rlImGui.Shutdown();
        R3D.Close();
        CloseWindow();

        return 0;
    }

    // retints the stock dark theme: every blue-hued accent color (tabs, headers,
    // buttons, checkmarks, docking preview...) is remapped to pinkish-red,
    // preserving saturation/brightness. Grays are untouched.
    static void ApplyEditorTheme()
    {
        var colors = ImGui.GetStyle().Colors;
        for (int i = 0; i < (int)ImGuiCol.COUNT; i++)
        {
            var c = colors[i];
            ImGui.ColorConvertRGBtoHSV(c.X, c.Y, c.Z, out float h, out float s, out float v);
            if (s > 0.15f && h > 0.5f && h < 0.75f)
            {
                ImGui.ColorConvertHSVtoRGB(0.97f, s, v, out float r, out float g, out float b);
                colors[i] = new System.Numerics.Vector4(r, g, b, c.W);
            }
        }
    }

    static void DrawMainMenuBar()
    {
        if (!ImGui.BeginMainMenuBar()) return;

        if (ImGui.BeginMenu("File"))
        {
            //TODO: wire up level/project file management
            ImGui.MenuItem("New Level", "Ctrl+N", false, false);
            ImGui.MenuItem("Open Level...", "Ctrl+O", false, false);
            ImGui.MenuItem("Save Level", "Ctrl+S", false, false);
            ImGui.Separator();
            if (ImGui.MenuItem("Exit", "Alt+F4"))
                _exit_requested = true;
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            //TODO: undo/redo stack
            ImGui.MenuItem("Undo", "Ctrl+Z", false, false);
            ImGui.MenuItem("Redo", "Ctrl+Y", false, false);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Window"))
        {
            if (ImGui.MenuItem("File Explorer")) OpenWindow<WND_FileExplorer>();
            if (ImGui.MenuItem("Asset Editor")) OpenWindow<WND_AssetEdit>();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"))
        {
            ImGui.MenuItem("Imperium Engine Editor", null, false, false);
            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

    static void OpenWindow<T>() where T : EditorWindow, new()
    {
        var existing = windows_open.OfType<T>().FirstOrDefault();
        if (existing != null)
        {
            ImGui.SetWindowFocus(existing.Title);
            return;
        }

        var w = new T();
        w.Open();
        windows_open.Add(w);
    }
}
