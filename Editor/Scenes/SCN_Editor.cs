using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Editor.Dialog;
using Editor.Panels;
using Editor.Windows;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using ImGuiNET;


namespace Editor.Scenes;


public class SCN_Editor : A_Scene
{
    public SCN_Editor()
    {
        root = new SNC_Editor_Root();
    }
}


public class SNC_Editor_Root : ImpComp
{
    public WND_Scene wnd_scene = new();
    public WND_Assets wnd_assets = new();
    public WND_ConfigGame wnd_config_game = new();
    public WND_ConfigEditor wnd_config_editor = new();
    public PNL_FileBrowser file_browser = new();
    public PNL_Log log_panel = new();

    static Process? _play_proc;
    static IntPtr _play_job;

    private EdWindow active_window;
    [EdConfig] public float file_browser_width = 280f;
    [EdConfig] public bool file_browser_collapsed;
    [EdConfig] public float log_height = 140f;

    public SNC_Editor_Root()
    {
        file_browser.on_open = OpenFromBrowser;
    }

    public void Draw_MainWindow(EdWindow w, string nam)
    {
        bool selected;
        if (nam == "Assets" && WND_Assets.request_focus)
        {
            WND_Assets.request_focus = false;
            bool stay = true;
            selected = ImGui.BeginTabItem(nam, ref stay, ImGuiTabItemFlags.SetSelected);
        }
        else
            selected = ImGui.BeginTabItem(nam);
        if (!selected) return;
        active_window = w;
        w.OnDraw();
        ImGui.EndTabItem();
    }

    public override void OnDraw2D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw2D(dt, flags);

        ImGuiViewportPtr vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.Pos);
        ImGui.SetNextWindowSize(vp.Size);
        ImGuiWindowFlags host_flags =
            ImGuiWindowFlags.NoTitleBar | 
            ImGuiWindowFlags.NoCollapse | 
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoBringToFrontOnFocus | 
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.MenuBar;
        ImGui.Begin("##editor", host_flags);
        // ------------------------------------------------------------------------------------------------------------
        // Menu Bar
        // ------------------------------------------------------------------------------------------------------------
        ImGui.BeginMainMenuBar();
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New Scene"))
            {
                wnd_scene.Scene_New();
            }
            if (ImGui.MenuItem("New Asset"))
            {
                
            }
            ImGui.Separator();
            if (ImGui.MenuItem("Save", "Ctrl+S")) Save(0);
            if (ImGui.MenuItem("Save As", "Ctrl+Shift+S")) Save(1);
            if (ImGui.MenuItem("Save All", "Ctrl+Alt+S")) Save(2);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo", "Ctrl+Z", false, Editor.history.CanUndo))
            {
                Editor.history.Undo();
            }
            if (ImGui.MenuItem("Redo", "Ctrl+Y / Ctrl+Shift+Z", false, Editor.history.CanRedo))
            {
                Editor.history.Redo();
            }
            ImGui.Separator();
            ImGui.EndMenu();
            
        }
        
        ImGui.EndMainMenuBar();
        // ------------------------------------------------------------------------------------------------------------
        // Main Buttons
        // ------------------------------------------------------------------------------------------------------------
        ImGui.Separator();

        bool playing = Play_IsRunning();
        bool MainBtn(string id, string label, bool selected = false)
        {
            return EdIcons.Button("##mb_" + id, label, EdIcons.Main(id), selected, true);
        }

        if (MainBtn("scene", "New Scene"))
            wnd_scene.Scene_New();
        ImGui.SameLine();
        if (MainBtn("asset", "New Asset"))
        {
        }
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        if (MainBtn("save", "Save")) Save(0);
        ImGui.SameLine();
        if (MainBtn("save_as", "Save As")) Save(1);
        ImGui.SameLine();
        if (MainBtn("save_all", "Save All")) Save(2);
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        if (MainBtn("play", "Play"))
            Play_Start(false);
        ImGui.SameLine();
        if (MainBtn("play_start", "Play (From Start)"))
            Play_Start(true);
        ImGui.SameLine();
        if (MainBtn("stop", "Stop", playing))
            Play_Stop();
        
        // ------------------------------------------------------------------------------------------------------------
        // File Browser (global left) + Main Tabs
        // ------------------------------------------------------------------------------------------------------------
        ImGui.Separator();
        float avail_x = ImGui.GetContentRegionAvail().X;
        float splitter = 6f;
        if (!file_browser_collapsed)
        {
            file_browser_width = Math.Clamp(file_browser_width, 180f, MathF.Max(180f, avail_x - 220f));
            ImGui.BeginChild("##fb_side", new Vector2(file_browser_width, 0), true);
            if (ImGui.ArrowButton("##fb_col", ImGuiDir.Left))
                file_browser_collapsed = true;
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Files");
            ImGui.Separator();
            file_browser.OnDrawPanel();
            ImGui.EndChild();

            ImGui.SameLine(0, 0);
            ImGui.InvisibleButton("##fb_split", new Vector2(splitter, ImGui.GetContentRegionAvail().Y));
            if (ImGui.IsItemActive())
                file_browser_width += ImGui.GetIO().MouseDelta.X;
            if (ImGui.IsItemHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
            ImGui.SameLine(0, 0);
        }
        else
        {
            ImGui.BeginChild("##fb_strip", new Vector2(26f, 0), true);
            if (ImGui.ArrowButton("##fb_exp", ImGuiDir.Right))
                file_browser_collapsed = false;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Files");
            ImGui.EndChild();
            ImGui.SameLine(0, 0);
        }

        ImGui.BeginChild("##main_tabs", Vector2.Zero, false);
        float log_split = 5f;
        float avail_y = ImGui.GetContentRegionAvail().Y;
        log_height = Math.Clamp(log_height, 60f, MathF.Max(60f, avail_y - 120f));
        float tabs_h = MathF.Max(80f, avail_y - log_height - log_split);

        ImGui.BeginChild("##tabs_host", new Vector2(0, tabs_h), false);
        ImGui.BeginTabBar("##tabs");
            Draw_MainWindow(wnd_scene, "Scene");
            Draw_MainWindow(wnd_assets, "Assets");
            Draw_MainWindow(wnd_config_game, "Game Config");
            Draw_MainWindow(wnd_config_editor, "Editor Config");
        ImGui.EndTabBar();
        ImGui.EndChild();

        ImGui.InvisibleButton("##log_split", new Vector2(MathF.Max(1f, ImGui.GetContentRegionAvail().X), log_split));
        if (ImGui.IsItemActive())
            log_height -= ImGui.GetIO().MouseDelta.Y;
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);

        ImGui.BeginChild("##log_host", new Vector2(0, log_height), true);
        log_panel.OnDrawPanel();
        ImGui.EndChild();
        ImGui.EndChild();

        EDLG_Confirm.DrawPending();
        EDLG_PickAsset.DrawPending();
        EDLG_PickClass.DrawPending();
        EDLG_SaveAsset.DrawPending();
        EDLG_CreateAsset.DrawPending();

        // ------------------------------------------------------------------------------------------------------------
        ImGui.End(); // Editor End

        bool ctrl = ImGui.GetIO().KeyCtrl;
        bool shift = ImGui.GetIO().KeyShift;
        if (ctrl && !ImGui.GetIO().WantTextInput)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Z) && !shift) Editor.history.Undo();
            if ((ImGui.IsKeyPressed(ImGuiKey.Z) && shift) || ImGui.IsKeyPressed(ImGuiKey.Y))
                Editor.history.Redo();
            if (ImGui.IsKeyPressed(ImGuiKey.S))
                wnd_scene.current_scene_tab?.scene?.Save(true);
        }
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        wnd_scene.Tick(dt);
    }

    public override void OnDraw3D(double dt, EDrawFlags flags = EDrawFlags.None)
    {
        base.OnDraw3D(dt, flags);
    }

    public ImpAsset GetCurrentEditedAsset()
    {
        return null;
    }

    //0=save | 1=save as | 2=save all
    public void Save(int type)
    {
        ImpAsset _ast = GetCurrentEditedAsset();
        if (_ast != null)
        {
            if (type == 1 || (type == 0 && _ast.filepath == null))
            {
                EDLG_SaveAsset.Run(_ast, (s) =>
                {
                    SaveConfirmation(_ast);
                });
            }
            else if (type == 0)
            {
                SaveConfirmation(_ast);
            }
            //save all
            if (type == 2)
            {
                
            }
        }
    }

    private void SaveConfirmation(ImpAsset asset)
    {
        
    }

    public void Play_Start(bool from_start)
    {
        Play_Stop();

        string name = OperatingSystem.IsWindows() ? "Game.exe" : "Game";
        string root = GFile.GetDir_Root(EContentDir.Engine);
#if DEBUG
        string cfg = "Debug";
#else
        string cfg = "Release";
#endif
        string exe = Path.Combine(AppContext.BaseDirectory, name);
        if (!File.Exists(exe))
            exe = Path.Combine(root, "bin", cfg, name);
        if (!File.Exists(exe))
        {
            string csproj = Path.Combine(root, "Game", "Game.csproj");
            if (!File.Exists(csproj))
            {
                GLog.Error("Game.exe not found.");
                return;
            }
            GLog.Info("Building Game...");
            try
            {
                Process build = Process.Start(new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build \"" + csproj + "\" -nologo -v q",
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                if (build == null)
                {
                    GLog.Error("dotnet build failed to start.");
                    return;
                }
                string stdout = build.StandardOutput.ReadToEnd();
                string stderr = build.StandardError.ReadToEnd();
                build.WaitForExit();
                if (build.ExitCode != 0)
                {
                    GLog.Error("Game build failed.");
                    if (!string.IsNullOrWhiteSpace(stderr)) GLog.Error(stderr.Trim());
                    if (!string.IsNullOrWhiteSpace(stdout)) GLog.Error(stdout.Trim());
                    return;
                }
            }
            catch (Exception e)
            {
                GLog.Error("Game build failed: " + e.Message);
                return;
            }
            exe = Path.Combine(root, "bin", cfg, name);
        }
        if (!File.Exists(exe))
        {
            GLog.Error("Game.exe not found.");
            return;
        }
        if (string.IsNullOrEmpty(App.game_file) || !File.Exists(App.game_file))
        {
            GLog.Error("No game project loaded.");
            return;
        }

        string args = "-game \"" + App.game_file + "\"";
        if (!from_start)
        {
            A_Scene? scene = wnd_scene.current_scene_tab?.scene;
            if (scene == null || string.IsNullOrEmpty(scene.filepath))
            {
                GLog.Error("Save the current scene before playing.");
                return;
            }
            if (scene.is_dity) scene.Save(true);
            args += " -scene \"" + GFile.Make_Path_Local(scene.filepath) + "\"";
            GLog.Info("Playing " + scene.filepath);
        }
        else
            GLog.Info("Playing from start");

        string workdir = Path.GetDirectoryName(exe) ?? "";
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (_play_job == IntPtr.Zero)
                {
                    _play_job = CreateJobObjectW(IntPtr.Zero, null);
                    if (_play_job != IntPtr.Zero)
                    {
                        JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new();
                        info.BasicLimitInformation.LimitFlags = 0x2000; // KILL_ON_JOB_CLOSE
                        int size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                        IntPtr ptr = Marshal.AllocHGlobal(size);
                        Marshal.StructureToPtr(info, ptr, false);
                        SetInformationJobObject(_play_job, 9, ptr, (uint)size);
                        Marshal.FreeHGlobal(ptr);
                    }
                }

                STARTUPINFO si = new() { cb = Marshal.SizeOf<STARTUPINFO>() };
                string cmdline = "\"" + exe + "\" " + args;
                string? dir = string.IsNullOrEmpty(workdir) ? null : workdir;
                uint flags = 0x00000010 | 0x01000000; // CREATE_NEW_CONSOLE | CREATE_BREAKAWAY_FROM_JOB
                if (!CreateProcessW(null, new StringBuilder(cmdline), IntPtr.Zero, IntPtr.Zero, false, flags, IntPtr.Zero, dir, ref si, out PROCESS_INFORMATION pi))
                {
                    si = new() { cb = Marshal.SizeOf<STARTUPINFO>() };
                    if (!CreateProcessW(null, new StringBuilder(cmdline), IntPtr.Zero, IntPtr.Zero, false, 0x00000010, IntPtr.Zero, dir, ref si, out pi))
                    {
                        GLog.Error("Failed to start Game.exe (" + Marshal.GetLastWin32Error() + ")");
                        return;
                    }
                }
                if (_play_job != IntPtr.Zero)
                    AssignProcessToJobObject(_play_job, pi.hProcess);
                _play_proc = Process.GetProcessById(pi.dwProcessId);
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
            }
            else
            {
                _play_proc = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = workdir,
                    UseShellExecute = false,
                });
            }
        }
        catch (Exception e)
        {
            GLog.Error("Failed to start Game.exe: " + e.Message);
            _play_proc = null;
        }
    }

    public static void Play_Stop()
    {
        if (_play_proc == null) return;
        try
        {
            if (!_play_proc.HasExited)
            {
                _play_proc.Kill(entireProcessTree: true);
                _play_proc.WaitForExit(2000);
            }
        }
        catch { }
        try { _play_proc.Dispose(); } catch { }
        _play_proc = null;
    }

    static bool Play_IsRunning()
    {
        try { return _play_proc != null && !_play_proc.HasExited; }
        catch { return false; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpInfo, uint cbInfo);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcessW(string? app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string? dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    void OpenFromBrowser(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        string ext = Path.GetExtension(path);
        if (!ext.Equals(".ImpScene", StringComparison.OrdinalIgnoreCase)) return;

        string local = GFile.Make_Path_Local(path);
        A_Scene? scene = GAsset.Asset_Load<A_Scene>(local);
        if (scene == null) return;
        wnd_scene.Scene_Open(scene);
    }
}