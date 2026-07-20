using Editor.Dialog;
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

    // the most recently focused window that has a document asset. Kept sticky so it stays the
    // Save target even while a menu/popup steals focus (menus report the window as unfocused).
    static EditorWindow? _active_doc_window;

    public static int Main()
    {
        string projectDir = ImperiumEngine.Program.FindProjectDir();
        string engineContentDir = ImperiumEngine.Program.FindEngineContentDir();

        Console.WriteLine($"[Editor] Project dir:        {projectDir}");
        Console.WriteLine($"[Editor] Engine content dir: {engineContentDir}");

        ImpAsset.s_projectDir = projectDir;
        ImpAsset.s_engineContentDir = engineContentDir;

        string configPath = EditorConfig.PathFor(projectDir);
        var config = EditorConfig.Load(configPath);

        SetConfigFlags(ConfigFlags.ResizableWindow);
        InitWindow(1600, 900, "Imperium Editor");
        SetTargetFPS(60);

        R3D.Init(GetScreenWidth(), GetScreenHeight());
        rlImGui.Setup(darkTheme: true, enableDocking: true);
        ApplyEditorTheme();

        // reopen the level from last session if it still exists, else the project default
        string levelPath = Path.Combine(projectDir, "Content", "Levels", "test.ImpLvl");
        if (config.last_level != "")
        {
            string resolved = ImpAsset.ResolvePath(config.last_level);
            if (File.Exists(resolved)) levelPath = resolved;
        }

        var level = new A_Level();
        level.File_Load(levelPath);

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

        // restore editor session state (camera, layout, tool windows)
        ApplyConfig(config);

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

            UpdateActiveDocWindow();
            HandleSaveHotkeys();

            EditorDialog.DrawActive(delta);   // modal popups draw on top of everything

            rlImGui.End();
            EndDrawing();
        }

        foreach (var c in level.components) c.OnEnd();

        CaptureConfig(config, level);
        config.Save(configPath);

        rlImGui.Shutdown();
        R3D.Close();
        CloseWindow();

        return 0;
    }

    // pushes saved session state into the live editor on startup
    static void ApplyConfig(EditorConfig config)
    {
        var w = window_main.pnl_world;
        w.cam_position = config.cam_position;
        w.cam_yaw = config.cam_yaw;
        w.cam_pitch = config.cam_pitch;
        w.cam_fov = config.cam_fov;

        window_main.SideWidth = config.side_width;
        window_main.SideSplit = config.side_split;

        foreach (string name in config.open_windows)
            OpenWindowByName(name);
    }

    // reads the live editor state back into the config just before it is written out
    static void CaptureConfig(EditorConfig config, A_Level level)
    {
        var w = window_main.pnl_world;
        config.cam_position = w.cam_position;
        config.cam_yaw = w.cam_yaw;
        config.cam_pitch = w.cam_pitch;
        config.cam_fov = w.cam_fov;

        config.side_width = window_main.SideWidth;
        config.side_split = window_main.SideSplit;

        config.last_level = ImpAsset.ToKeywordPath(level.file_link);

        config.open_windows = windows_open
            .Where(x => x != window_main)
            .Select(x => x.GetType().Name)
            .ToList();
    }

    // opens a tool window by its type name (used to restore last session's windows)
    static void OpenWindowByName(string typeName)
    {
        if (typeName == nameof(WND_LevelEdit)) return;   // the level editor is always the main window

        var type = typeof(Program).Assembly.GetTypes().FirstOrDefault(t =>
            t.Name == typeName && typeof(EditorWindow).IsAssignableFrom(t) &&
            !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);
        if (type == null || windows_open.Any(x => x.GetType() == type)) return;

        var win = (EditorWindow)Activator.CreateInstance(type)!;
        win.Open();
        windows_open.Add(win);
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
            ImGui.Separator();

            var doc = _active_doc_window?.DocumentAsset;
            string docName = doc != null ? DocLabel(_active_doc_window!) : "";
            if (ImGui.MenuItem(doc != null ? $"Save {docName}" : "Save", "Ctrl+S", false, doc != null))
                SaveActive(force_dialog: false);
            if (ImGui.MenuItem("Save As...", "Ctrl+Shift+S", false, doc != null))
                SaveActive(force_dialog: true);
            if (ImGui.MenuItem("Save All", null, false, AnyDirty()))
                SaveAllAssets();
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
            ImGui.SetWindowFocus(existing.WindowId);   // id, not the (dynamic) visible title
            return;
        }

        var w = new T();
        w.Open();
        windows_open.Add(w);
    }

    // ----------------------------------------------------------------
    // Saving
    // ----------------------------------------------------------------

    // tracks which document window is the Save target. Sticky: only updates when a document
    // window is genuinely focused, so opening a menu doesn't drop the active document.
    static void UpdateActiveDocWindow()
    {
        var focused = windows_open.FirstOrDefault(w => w.has_focus && w.DocumentAsset != null);
        if (focused != null) _active_doc_window = focused;
        if (_active_doc_window != null && !windows_open.Contains(_active_doc_window))
            _active_doc_window = null;
    }

    // S / Ctrl+S = Save active document (prompts for a location only if it has no file yet),
    // Ctrl+Shift+S = Save As (always prompts). Suppressed while a modal/menu owns input, while
    // typing into a field, or while fly-controlling the viewport (RMB held, S = move back).
    static void HandleSaveHotkeys()
    {
        var io = ImGui.GetIO();
        if (EditorDialog.AnyActive || io.WantTextInput) return;
        if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)) return;
        if (ImGui.IsMouseDown(ImGuiMouseButton.Right)) return;

        if (!ImGui.IsKeyPressed(ImGuiKey.S, false)) return;
        if (io.KeyAlt || io.KeySuper) return;

        SaveActive(force_dialog: io.KeyCtrl && io.KeyShift);   // Ctrl+Shift+S -> Save As
    }

    // Saves the active document window's asset. `force_dialog` (Save As) always opens the
    // location picker; otherwise an asset with no backing file opens it, one with a file saves
    // straight through the async progress dialog.
    static void SaveActive(bool force_dialog)
    {
        var asset = _active_doc_window?.DocumentAsset;
        if (asset == null) return;

        if (force_dialog || !asset.IsReference)
        {
            DLG_SaveAsset.Show(asset, on_saved: _ => asset.is_dirty = false);
            return;
        }
        RunSave([asset]);
    }

    // Saves every dirty tracked document. Any that still lack a file get a location picker
    // first (one at a time), then the rest are written together through the progress dialog.
    static void SaveAllAssets()
    {
        var dirty = DirtyDocuments();
        if (dirty.Count == 0) return;

        var needs_path = dirty.FirstOrDefault(a => !a.IsReference);
        if (needs_path != null)
        {
            DLG_SaveAsset.Show(needs_path, on_saved: _ =>
            {
                needs_path.is_dirty = false;
                SaveAllAssets();   // continue with whatever is still dirty
            });
            return;
        }
        RunSave(dirty);
    }

    // Writes the given assets to their existing files on a background thread, showing a progress
    // dialog. The modal blocks further editing so the worker reads stable asset state.
    static void RunSave(IReadOnlyList<ImpAsset> assets)
    {
        if (assets.Count == 0) return;
        var list = assets.ToArray();

        DLG_Process.Run($"Saving {list.Length} asset{(list.Length == 1 ? "" : "s")}…", report =>
        {
            bool all_ok = true;
            for (int i = 0; i < list.Length; i++)
            {
                var asset = list[i];
                report(i / (float)list.Length, $"Saving {AssetName(asset)}…");

                if (asset.File_Save(asset.file_link)) asset.is_dirty = false;
                else all_ok = false;

                Thread.Sleep(40);   // saves are near-instant; keep the bar readable
            }
            report(1f, "Done");
            return all_ok;
        });
    }

    // every dirty asset the editor is tracking (the open document windows' assets)
    static List<ImpAsset> DirtyDocuments() => windows_open
        .Select(w => w.DocumentAsset)
        .Where(a => a is { is_dirty: true })
        .Cast<ImpAsset>()
        .Distinct()
        .ToList();

    static bool AnyDirty() => windows_open.Any(w => w.DocumentAsset is { is_dirty: true });

    // label for a document in the File menu: its file name if saved, else the asset type
    static string DocLabel(EditorWindow w)
    {
        var a = w.DocumentAsset!;
        return a.IsReference ? Path.GetFileNameWithoutExtension(a.file_link) : AssetName(a);
    }

    static string AssetName(ImpAsset a) =>
        a.IsReference ? Path.GetFileName(a.file_link) : a.GetType().Name;
}
