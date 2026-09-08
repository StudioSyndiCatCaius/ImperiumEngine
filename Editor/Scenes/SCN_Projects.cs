using System.Numerics;
using Editor.Dialog;
using Editor.Panels;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Scenes;

/*
 *  STARING EDITOR "SCENE". lets you pick or create a project to edit
 *  LAYOUT:
 *      left: 
 *          - top: buttons (New (open file dialog for where to create), Add (add existing), Remove (requires confirm. just removes from list, doesn't delete))
 *          - bottom: list of projects
 *      right:
 *          - inspector for currently selected project
 *          - "Edit" button
 *  Layout has 2 tabs : "Projects" and "Config"
*/
public class SCN_Projects : A_Scene
{
    // ========================================================================
    // Global Editor Config
    // ========================================================================
    [ImpVar][Config][Title("Autoload Last Project")]
    public bool autoload_last_project = false;
    [ImpVar][Config][Title("Last Project")]
    public string last_project_path = "";

    // ========================================================================
    // ========================================================================

    public SCN_Projects()
    {
        root = new SNC_Projects_Root(this);
    }

    public static string GlobalConfigPath()
    {
        string root = GFile.GetDir_Root(EContentDir.Engine);
        if (string.IsNullOrEmpty(root)) return "";
        return Path.Combine(root, "Config", "Editor.toml");
    }

    public void LoadGlobal()
    {
        string path = GlobalConfigPath();
        bool seed = string.IsNullOrEmpty(path) || !File.Exists(path);
        TTable tbl = new();
        if (!seed)
        {
            try { tbl = TTable.FromTOML(File.ReadAllText(path)); }
            catch (Exception e)
            {
                GLog.Warning("Editor.toml load failed: " + e.Message);
                tbl = new();
            }
        }

        autoload_last_project = tbl.get_Bool("autoload_last_project", autoload_last_project);
        last_project_path = tbl.get_String("last_project_path", last_project_path);
        if (root is SNC_Projects_Root r)
            r.LoadFrom(tbl, seed);
        if (seed) SaveGlobal();
    }

    public void SaveGlobal()
    {
        string path = GlobalConfigPath();
        if (string.IsNullOrEmpty(path)) return;

        TTable tbl = new();
        tbl.Set("autoload_last_project", autoload_last_project);
        tbl.Set("last_project_path", last_project_path ?? "");
        if (root is SNC_Projects_Root r)
            r.StoreTo(tbl);

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, TTable.ToTOML(tbl));
    }

    public static void OpenProject(string game_path)
    {
        string resolved = SNC_Projects_Root.ResolveGameFile(game_path);
        if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved))
        {
            GLog.Error("Project not found: " + game_path);
            return;
        }

        if (App.scene_current?.root is SNC_Editor_Root editor)
            EdConfig.Save(editor);
        if (App.scene_current?.root is SNC_Projects_Root picker)
            picker.UnloadThumbs();

        if (App.scene_current is SCN_Projects proj)
        {
            proj.last_project_path = resolved;
            if (proj.root is SNC_Projects_Root r)
                r.AddExisting(resolved, false);
            proj.SaveGlobal();
        }
        else
            PatchLastPath(resolved);

        App.game_file = resolved;
        try { App.game_data = TTable.FromTOML(GFile.LoadAs_String(resolved)); }
        catch (Exception e)
        {
            GLog.Warning("ImpGame load failed: " + e.Message);
            App.game_data = new();
        }
        GConfig.LoadGame();

        SCN_Editor next = new();
        App.scene_current = next;
        if (next.root is SNC_Editor_Root root)
            EdConfig.Load(root);

        string name = App.game_data.get_String("name");
        if (string.IsNullOrEmpty(name))
            name = Path.GetFileNameWithoutExtension(resolved);
        Raylib.SetWindowTitle("Imperium Editor - " + name);
        GLog.Info("Opened project " + resolved);
    }

    static void PatchLastPath(string game_path)
    {
        string path = GlobalConfigPath();
        TTable tbl = new();
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try { tbl = TTable.FromTOML(File.ReadAllText(path)); }
            catch { tbl = new(); }
        }
        tbl.Set("last_project_path", game_path);
        List<object> list = tbl.get_List("projects");
        bool found = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i]?.ToString(), game_path, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }
        if (!found) list.Add(game_path);
        tbl.Set("projects", list);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        if (!string.IsNullOrEmpty(path))
            File.WriteAllText(path, TTable.ToTOML(tbl));
    }
}

[Title("Project")]
public class EdProject
{
    public string path = "";
    [ImpVar][Title("Name")] public string name = "";
    [ImpVar][Title("Starting Scene")] public string starting_scene = "";
    public Texture2D thumb;
    public bool missing;
}

public class SNC_Projects_Root : ImpComp
{
    public SCN_Projects host;
    public PNL_Inspector project_inspector = new();
    public PNL_Inspector config_inspector = new();
    public float left_panel_width = 380f;

    readonly List<EdProject> projects = new();
    int selected = -1;
    protected override ECompProcess ProcessKinds => ECompProcess.Update | ECompProcess.Draw2D;

    public SNC_Projects_Root(SCN_Projects host)
    {
        this.host = host;
        project_inspector.on_changed = WriteSelected;
        config_inspector.config_only = true;
        config_inspector.on_changed = () => host.SaveGlobal();
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
            ImGuiWindowFlags.NoDocking;
        ImGui.Begin("##projects", host_flags);

        ImGui.TextUnformatted("Imperium Editor");
        ImGui.SameLine();
        ImGui.TextDisabled("v" + App.version);
        ImGui.Separator();

        if (ImGui.BeginTabBar("##proj_tabs"))
        {
            if (ImGui.BeginTabItem("Projects"))
            {
                DrawProjects();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Config"))
            {
                config_inspector.selected_object = host;
                config_inspector.OnDrawPanel();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        EDLG_Confirm.DrawPending();
        EDLG_FileAction.DrawPending();
        ImGui.End();
    }

    void DrawProjects()
    {
        float avail_x = ImGui.GetContentRegionAvail().X;
        float splitter = 6f;
        left_panel_width = Math.Clamp(left_panel_width, 220f, MathF.Max(220f, avail_x - 220f));

        ImGui.BeginChild("##proj_left", new Vector2(left_panel_width, 0), true);
        DrawButtons();
        ImGui.Separator();
        ImGui.BeginChild("##proj_list", Vector2.Zero, false);
        DrawList();
        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.SameLine(0, 0);
        ImGui.InvisibleButton("##proj_split", new Vector2(splitter, ImGui.GetContentRegionAvail().Y));
        if (ImGui.IsItemActive())
            left_panel_width += ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);

        ImGui.SameLine(0, 0);
        ImGui.BeginChild("##proj_right", Vector2.Zero, true);
        DrawInspector();
        ImGui.EndChild();
    }

    void DrawButtons()
    {
        if (ImGui.Button("New"))
        {
            string dir = DefaultProjectsDir();
            EDLG_FileAction.Run(new EDLG_FileAction
            {
                type = EFileActionType.Save,
                root_path = dir,
                filename = "NewProject.ImpGame",
                extensions = new List<string> { ".ImpGame" },
            }, path =>
            {
                if (!string.IsNullOrEmpty(path))
                    CreateNew(path);
            }, dir);
        }
        ImGui.SameLine();
        if (ImGui.Button("Add"))
        {
            string dir = DefaultProjectsDir();
            EDLG_FileAction.Run(new EDLG_FileAction
            {
                type = EFileActionType.Open,
                root_path = dir,
                extensions = new List<string> { ".ImpGame" },
            }, path =>
            {
                if (!string.IsNullOrEmpty(path))
                    AddExisting(path, true);
            }, dir);
        }
        ImGui.SameLine();
        bool can_remove = selected >= 0 && selected < projects.Count;
        if (!can_remove) ImGui.BeginDisabled();
        if (ImGui.Button("Remove"))
            ConfirmRemove();
        if (!can_remove) ImGui.EndDisabled();
    }

    void DrawList()
    {
        float thumb = 56f;
        float pad = 6f;
        float row_h = thumb + pad * 2f;

        for (int i = 0; i < projects.Count; i++)
        {
            EdProject p = projects[i];
            ImGui.PushID(p.path + i);
            bool is_sel = i == selected;
            if (is_sel)
                ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
            bool hit = ImGui.Selectable("##row", is_sel, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(0, row_h));
            if (is_sel) ImGui.PopStyleColor();
            if (hit) selected = i;
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                selected = i;
                EditSelected();
            }

            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            Vector2 tmin = new(min.X + pad, min.Y + pad);
            Vector2 tmax = new(tmin.X + thumb, tmin.Y + thumb);
            dl.AddRectFilled(tmin, tmax, ImGui.GetColorU32(new Vector4(0.12f, 0.13f, 0.16f, 1f)), 3f);
            if (p.thumb.Id != 0)
            {
                Vector2 imin = tmin + new Vector2(2f, 2f);
                Vector2 imax = tmax - new Vector2(2f, 2f);
                Fit(p.thumb.Width, p.thumb.Height, ref imin, ref imax);
                dl.AddImage((IntPtr)p.thumb.Id, imin, imax);
            }
            else
            {
                string mark = p.missing ? "?" : "P";
                Vector2 sz = ImGui.CalcTextSize(mark);
                dl.AddText(new Vector2(
                    tmin.X + (thumb - sz.X) * 0.5f,
                    tmin.Y + (thumb - sz.Y) * 0.5f),
                    ImGui.GetColorU32(ImGuiCol.TextDisabled), mark);
            }

            float tx = tmax.X + 10f;
            float ty = min.Y + pad;
            uint name_col = p.missing
                ? ImGui.GetColorU32(ImGuiCol.TextDisabled)
                : ImGui.GetColorU32(ImGuiCol.Text);
            dl.AddText(new Vector2(tx, ty), name_col, string.IsNullOrEmpty(p.name) ? "(unnamed)" : p.name);
            string sub = p.missing ? "(missing)  " + p.path : p.path;
            dl.AddText(new Vector2(tx, ty + ImGui.GetTextLineHeight() + 2f),
                ImGui.GetColorU32(ImGuiCol.TextDisabled), sub);
            ImGui.PopID();
        }

        if (projects.Count == 0)
            ImGui.TextDisabled("No projects. New or Add to begin.");

        if (!ImGui.GetIO().WantTextInput && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
                EditSelected();
            if (ImGui.IsKeyPressed(ImGuiKey.Delete))
                ConfirmRemove();
        }
    }

    void DrawInspector()
    {
        EdProject? p = Selected();
        float btn_h = ImGui.GetFrameHeight() * 1.6f;
        float gap = ImGui.GetStyle().ItemSpacing.Y;
        float body_h = MathF.Max(40f, ImGui.GetContentRegionAvail().Y - btn_h - gap);

        ImGui.BeginChild("##proj_insp", new Vector2(0, body_h), false);
        if (p == null)
            ImGui.TextDisabled("No project selected");
        else
        {
            if (p.thumb.Id != 0)
            {
                float preview = MathF.Min(180f, ImGui.GetContentRegionAvail().X);
                float aspect = p.thumb.Height > 0 ? p.thumb.Width / (float)p.thumb.Height : 1f;
                Vector2 sz = aspect >= 1f
                    ? new Vector2(preview, preview / aspect)
                    : new Vector2(preview * aspect, preview);
                Vector2 pos = ImGui.GetCursorScreenPos();
                ImGui.Dummy(sz);
                ImGui.GetWindowDrawList().AddImage((IntPtr)p.thumb.Id, pos, pos + sz);
            }
            ImGui.TextDisabled(p.path);
            ImGui.Separator();
            project_inspector.selected_object = p;
            project_inspector.OnDrawPanel();
        }
        ImGui.EndChild();

        bool can_edit = p != null && !p.missing && File.Exists(p.path);
        if (!can_edit) ImGui.BeginDisabled();
        if (ImGui.Button("Edit", new Vector2(-1, btn_h)))
            EditSelected();
        if (!can_edit) ImGui.EndDisabled();
    }

    EdProject? Selected()
    {
        if (selected < 0 || selected >= projects.Count) return null;
        return projects[selected];
    }

    void EditSelected()
    {
        EdProject? p = Selected();
        if (p == null || p.missing) return;
        SCN_Projects.OpenProject(p.path);
    }

    void ConfirmRemove()
    {
        EdProject? p = Selected();
        if (p == null) return;
        string label = string.IsNullOrEmpty(p.name) ? p.path : p.name;
        EDLG_Confirm.Run("Remove '" + label + "' from the list?\nThe project files will not be deleted.", ok =>
        {
            if (!ok) return;
            int i = selected;
            if (i < 0 || i >= projects.Count) return;
            UnloadThumb(projects[i]);
            projects.RemoveAt(i);
            if (selected >= projects.Count) selected = projects.Count - 1;
            host.SaveGlobal();
        });
    }

    void CreateNew(string raw)
    {
        string path = raw ?? "";
        if (!path.EndsWith(".ImpGame", StringComparison.OrdinalIgnoreCase))
            path += ".ImpGame";
        path = Path.GetFullPath(path);

        if (File.Exists(path))
        {
            AddExisting(path, true);
            return;
        }

        string? dir = Path.GetDirectoryName(path);
        string stem = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(stem))
        {
            GLog.Error("Invalid project path.");
            return;
        }

        try
        {
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "Config"));
            Directory.CreateDirectory(Path.Combine(dir, "Content"));
            Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));

            TTable game = new();
            game.Set("name", stem);
            game.Set("starting_scene", "");
            File.WriteAllText(path, TTable.ToTOML(game));

            string png = Path.Combine(dir, stem + ".png");
            if (!File.Exists(png))
            {
                string src = GFile.Make_Path_Absolute("{engine}/2D/UI/UI_Engine_Logo.png");
                if (string.IsNullOrEmpty(src) || !File.Exists(src))
                    src = GFile.Make_Path_Absolute("{engine}/Editor/Types/ImpAsset.png");
                if (!string.IsNullOrEmpty(src) && File.Exists(src))
                    File.Copy(src, png, false);
            }

            string tmpl = Path.Combine(GFile.GetDir_Root(EContentDir.Engine), "Templates", "Test", "Config", "Game.toml");
            string dest_cfg = Path.Combine(dir, "Config", "Game.toml");
            if (File.Exists(tmpl) && !File.Exists(dest_cfg))
                File.Copy(tmpl, dest_cfg, false);
        }
        catch (Exception e)
        {
            GLog.Error("Failed to create project: " + e.Message);
            return;
        }

        AddExisting(path, true);
        GLog.Info("Created project " + path);
    }

    public void AddExisting(string raw, bool save)
    {
        string path = ResolveGameFile(raw);
        if (string.IsNullOrEmpty(path))
        {
            GLog.Error("Not a valid ImpGame project: " + raw);
            return;
        }

        for (int i = 0; i < projects.Count; i++)
        {
            if (string.Equals(projects[i].path, path, StringComparison.OrdinalIgnoreCase))
            {
                selected = i;
                RefreshEntry(projects[i]);
                if (save) host.SaveGlobal();
                return;
            }
        }

        EdProject p = new() { path = path };
        RefreshEntry(p);
        projects.Add(p);
        selected = projects.Count - 1;
        if (save) host.SaveGlobal();
    }

    public void LoadFrom(TTable tbl, bool seed)
    {
        left_panel_width = tbl.get_Float("left_panel_width", left_panel_width);
        UnloadThumbs();
        projects.Clear();
        selected = -1;

        List<object> list = tbl.get_List("projects");
        for (int i = 0; i < list.Count; i++)
        {
            string path = list[i]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(path)) continue;
            EdProject p = new() { path = Path.GetFullPath(path) };
            RefreshEntry(p);
            projects.Add(p);
        }

        if (seed)
            SeedMyProjects();

        if (!string.IsNullOrEmpty(host.last_project_path))
        {
            for (int i = 0; i < projects.Count; i++)
            {
                if (string.Equals(projects[i].path, host.last_project_path, StringComparison.OrdinalIgnoreCase))
                {
                    selected = i;
                    break;
                }
            }
        }
        if (selected < 0 && projects.Count > 0) selected = 0;
    }

    public void StoreTo(TTable tbl)
    {
        tbl.Set("left_panel_width", left_panel_width);
        List<object> list = new();
        for (int i = 0; i < projects.Count; i++)
            list.Add(projects[i].path);
        tbl.Set("projects", list);
    }

    void SeedMyProjects()
    {
        string root = Path.Combine(GFile.GetDir_Root(EContentDir.Engine), "MyProjects");
        if (!Directory.Exists(root)) return;
        foreach (string dir in Directory.GetDirectories(root))
        {
            string[] games;
            try { games = Directory.GetFiles(dir, "*.ImpGame"); }
            catch { continue; }
            if (games.Length == 0) continue;
            AddExisting(games[0], false);
        }
    }

    void RefreshEntry(EdProject p)
    {
        UnloadThumb(p);
        p.missing = string.IsNullOrEmpty(p.path) || !File.Exists(p.path);
        if (p.missing)
        {
            if (string.IsNullOrEmpty(p.name))
                p.name = Path.GetFileNameWithoutExtension(p.path) ?? "";
            return;
        }

        try
        {
            p.path = Path.GetFullPath(p.path);
            TTable tbl = TTable.FromTOML(File.ReadAllText(p.path));
            p.name = tbl.get_String("name");
            p.starting_scene = tbl.get_String("starting_scene");
        }
        catch { }

        if (string.IsNullOrEmpty(p.name))
            p.name = Path.GetFileNameWithoutExtension(p.path) ?? "";

        string? dir = Path.GetDirectoryName(p.path);
        string stem = Path.GetFileNameWithoutExtension(p.path) ?? "";
        if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(stem))
        {
            string png = Path.Combine(dir, stem + ".png");
            if (File.Exists(png))
                p.thumb = LoadThumb(png);
        }
    }

    void WriteSelected()
    {
        EdProject? p = Selected();
        if (p == null || p.missing || string.IsNullOrEmpty(p.path)) return;
        TTable tbl = new();
        if (File.Exists(p.path))
        {
            try { tbl = TTable.FromTOML(File.ReadAllText(p.path)); }
            catch { tbl = new(); }
        }
        tbl.Set("name", p.name ?? "");
        tbl.Set("starting_scene", p.starting_scene ?? "");
        try { File.WriteAllText(p.path, TTable.ToTOML(tbl)); }
        catch (Exception e) { GLog.Error("Failed to save ImpGame: " + e.Message); }
    }

    public void UnloadThumbs()
    {
        for (int i = 0; i < projects.Count; i++)
            UnloadThumb(projects[i]);
    }

    static void UnloadThumb(EdProject p)
    {
        if (p.thumb.Id == 0) return;
        try { Raylib.UnloadTexture(p.thumb); } catch { }
        p.thumb = default;
    }

    static Texture2D LoadThumb(string file)
    {
        try
        {
            Image img = Raylib.LoadImage(file);
            if (img.Width <= 0 || img.Height <= 0)
            {
                Raylib.UnloadImage(img);
                return default;
            }
            const int maxd = 256;
            if (img.Width > maxd || img.Height > maxd)
            {
                float s = maxd / (float)Math.Max(img.Width, img.Height);
                Raylib.ImageResize(ref img,
                    Math.Max(1, (int)(img.Width * s)),
                    Math.Max(1, (int)(img.Height * s)));
            }
            Texture2D tex = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            return tex;
        }
        catch { return default; }
    }

    static void Fit(int w, int h, ref Vector2 min, ref Vector2 max)
    {
        if (w <= 0 || h <= 0) return;
        float aspect = (float)w / h;
        float bw = max.X - min.X;
        float bh = max.Y - min.Y;
        if (aspect > 1f)
        {
            float nh = bw / aspect;
            float y = min.Y + (bh - nh) * 0.5f;
            min.Y = y;
            max.Y = y + nh;
        }
        else
        {
            float nw = bh * aspect;
            float x = min.X + (bw - nw) * 0.5f;
            min.X = x;
            max.X = x + nw;
        }
    }

    public static string ResolveGameFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try
        {
            path = path.Trim().Trim('"');
            if (File.Exists(path) && path.EndsWith(".ImpGame", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(path);
            if (Directory.Exists(path))
            {
                string named = Path.Combine(path, Path.GetFileName(path.TrimEnd('\\', '/')) + ".ImpGame");
                if (File.Exists(named)) return Path.GetFullPath(named);
                string[] games = Directory.GetFiles(path, "*.ImpGame");
                if (games.Length > 0) return Path.GetFullPath(games[0]);
            }
        }
        catch { }
        return "";
    }

    static string DefaultProjectsDir()
    {
        string dir = Path.Combine(GFile.GetDir_Root(EContentDir.Engine), "MyProjects");
        return Directory.Exists(dir) ? dir : GFile.GetDir_Root(EContentDir.Engine);
    }
}
