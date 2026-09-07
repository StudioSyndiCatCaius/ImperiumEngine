using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Editor.Dialog;
using Editor.UI;
using Engine;
using Engine.Assets;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;
using Raylib_cs;

namespace Editor.Panels;

public class PNL_FileBrowser : EdPanel
{
    // settings
    [EdConfig] public bool show_editor_content = false;
    [EdConfig] public bool show_source_files = true;
    [EdConfig] public bool show_file_extensions = true;
    [EdConfig] public float thumbnail_size = 50;

    public string root_path = "";
    public string filter_string = "";
    public Action<string>? on_open;

    EUI_SearchBar _search = new() { hint = "Filter" };
    int _tab;
    string _selected_path = "";
    float _tree_h = 180f;

    string _rename_path = "";
    string _rename_buf = "";
    bool _rename_focus;
    readonly Dictionary<string, Vector3?> _folder_colors = new(StringComparer.OrdinalIgnoreCase);

    public PNL_FileBrowser()
    {
        title = "Files";
        _search.on_search = s => filter_string = s ?? "";
    }

    public override void OnDrawPanel()
    {
        base.OnDrawPanel();

        _search.search_text = filter_string ?? "";
        _search.OnDraw();

        if (!show_editor_content) _tab = 0;

        float settings_h = 118f;
        float splitter = 5f;
        float avail_y = ImGui.GetContentRegionAvail().Y;
        float rest = MathF.Max(80f, avail_y - settings_h - splitter);
        _tree_h = Math.Clamp(_tree_h, 70f, MathF.Max(70f, rest - 70f));
        float grid_h = MathF.Max(60f, rest - _tree_h);

        ImGui.BeginChild("##fb_tree", new Vector2(0, _tree_h), true);
        if (show_editor_content && ImGui.BeginTabBar("##fb_tabs"))
        {
            if (ImGui.BeginTabItem("Game"))
            {
                _tab = 0;
                DrawTree();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Editor"))
            {
                _tab = 1;
                DrawTree();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        else
        {
            DrawTree();
        }
        if (ImGui.BeginPopupContextWindow("##fb_tree_empty", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
        {
            DrawEmptyMenu();
            ImGui.EndPopup();
        }
        ImGui.EndChild();

        ImGui.InvisibleButton("##fb_split", new Vector2(MathF.Max(1f, ImGui.GetContentRegionAvail().X), splitter));
        if (ImGui.IsItemActive())
            _tree_h += ImGui.GetIO().MouseDelta.Y;
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);

        string crumb = RelPath(root_path, ContentRoot());
        ImGui.TextDisabled(string.IsNullOrEmpty(crumb) ? "/" : crumb);

        ImGui.BeginChild("##fb_grid", new Vector2(0, grid_h), true);
        DrawGrid();
        if (ImGui.BeginPopupContextWindow("##fb_grid_empty", ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight))
        {
            DrawEmptyMenu();
            ImGui.EndPopup();
        }
        ImGui.EndChild();

        ImGui.SeparatorText("Settings");
        ImGui.Checkbox("Editor content", ref show_editor_content);
        ImGui.Checkbox("Source files", ref show_source_files);
        ImGui.Checkbox("Extensions", ref show_file_extensions);
        ImGui.SetNextItemWidth(-1);
        ImGui.SliderFloat("##thumb", ref thumbnail_size, 32f, 128f, "Thumb %.0f");

        bool hot = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
            || ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        if (hot && !ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey.F2))
            BeginRename(_selected_path);
    }

    string ContentRoot()
    {
        return GFile.GetDir_Content(_tab == 0 ? EContentDir.Game : EContentDir.Engine);
    }

    void DrawTree()
    {
        string content_root = ContentRoot();
        if (string.IsNullOrEmpty(root_path) || !IsUnder(root_path, content_root))
            root_path = Directory.Exists(content_root) ? content_root : "";
        if (!Directory.Exists(content_root))
        {
            ImGui.TextDisabled(_tab == 0 ? "No game project" : "No editor content");
            return;
        }
        DrawDirNode(content_root, true);
    }

    void DrawDirNode(string dir, bool is_root)
    {
        string name = is_root ? (_tab == 0 ? "Game" : "Editor") : Path.GetFileName(dir);
        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth;
        if (is_root) flags |= ImGuiTreeNodeFlags.DefaultOpen;
        if (PathsEqual(dir, root_path) || PathsEqual(dir, _selected_path)) flags |= ImGuiTreeNodeFlags.Selected;

        bool renaming = PathsEqual(dir, _rename_path);
        ImGui.PushID(dir);
        bool open = ImGui.TreeNodeEx("##d", flags, renaming ? "" : (name ?? ""));
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            root_path = dir;
            _selected_path = dir;
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            _selected_path = dir;

        Vector3? tint = GetFolderColor(dir);
        if (tint != null)
        {
            Vector2 min = ImGui.GetItemRectMin();
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            float h = ImGui.GetItemRectSize().Y;
            Vector4 c = new(tint.Value.X, tint.Value.Y, tint.Value.Z, 1f);
            dl.AddRectFilled(min, min + new Vector2(4f, h), ImGui.ColorConvertFloat4ToU32(c));
        }

        bool shown_in_grid = !is_root && PathsEqual(Path.GetDirectoryName(dir) ?? "", root_path);
        if (renaming && !shown_in_grid)
        {
            ImGui.SameLine();
            DrawRenameField(MathF.Max(80f, ImGui.GetContentRegionAvail().X));
        }
        else if (!renaming && ImGui.BeginPopupContextItem("##fb_dir"))
        {
            DrawFolderMenu(dir);
            ImGui.EndPopup();
        }

        if (open)
        {
            try
            {
                string[] subs = Directory.GetDirectories(dir);
                Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < subs.Length; i++)
                {
                    if (IsHidden(subs[i])) continue;
                    DrawDirNode(subs[i], false);
                }
            }
            catch { }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    void DrawGrid()
    {
        string dir = root_path;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            ImGui.TextDisabled("No folder");
            return;
        }

        float size = Math.Clamp(thumbnail_size, 32f, 128f);
        float gap = ImGui.GetStyle().ItemSpacing.X + 8f;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap));
        int cols = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + gap) / (size + gap)));
        int col = 0;

        void Emit(EUI_FileThumbnail thumb)
        {
            if (col > 0) ImGui.SameLine();
            thumb.size = size;
            thumb.show_ext = show_file_extensions;
            thumb.is_selected = PathsEqual(thumb.path, _selected_path);
            thumb.renaming = PathsEqual(thumb.path, _rename_path) && thumb.label_override != "..";
            thumb.rename_buf = _rename_buf;
            thumb.rename_focus = _rename_focus;
            if (thumb.is_folder)
            {
                Vector3? tint = GetFolderColor(thumb.path);
                if (tint != null)
                    thumb.color_override = new Vector4(tint.Value.X, tint.Value.Y, tint.Value.Z, 0.55f);
            }
            thumb.on_click = t => _selected_path = t.path ?? "";
            thumb.on_double_click = t =>
            {
                if (t.renaming) return;
                if (t.is_folder)
                {
                    if (Directory.Exists(t.path)) root_path = t.path;
                }
                else if (!string.IsNullOrEmpty(t.path))
                    on_open?.Invoke(t.path);
            };
            thumb.on_popup = t =>
            {
                if (t.label_override == "..") return;
                if (t.is_folder) DrawFolderMenu(t.path);
                else DrawFileMenu(t.path, t.type);
            };
            thumb.on_rename = CommitRename;
            thumb.on_rename_cancel = () => { _rename_path = ""; };
            thumb.OnDraw();
            if (thumb.renaming)
            {
                _rename_buf = thumb.rename_buf;
                _rename_focus = false;
            }
            col = (col + 1) % cols;
        }

        string content_root = ContentRoot();
        DirectoryInfo? parent = Directory.GetParent(dir);
        if (parent != null && IsUnder(parent.FullName, content_root) && !PathsEqual(dir, content_root))
        {
            Emit(new EUI_FileThumbnail
            {
                path = parent.FullName,
                is_folder = true,
                label_override = ".."
            });
        }

        try
        {
            string[] dirs = Directory.GetDirectories(dir);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (IsHidden(dirs[i]) || !NameMatch(dirs[i])) continue;
                Emit(new EUI_FileThumbnail { path = dirs[i], is_folder = true });
            }

            string[] files = Directory.GetFiles(dir);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                if (IsHidden(files[i]) || !NameMatch(files[i])) continue;
                bool source = IsSource(files[i]);
                if (source && !show_source_files) continue;
                Emit(new EUI_FileThumbnail
                {
                    path = files[i],
                    is_folder = false,
                    type = source ? EEditorFileType.SourceFile : EEditorFileType.Asset
                });
            }
        }
        catch { }
        ImGui.PopStyleVar();
    }

    void DrawEmptyMenu()
    {
        if (ImGui.MenuItem("New Folder")) NewFolder();
        if (ImGui.MenuItem("New Asset")) NewAsset();
        if (ImGui.MenuItem("New Scene")) NewScene();
        ImGui.Separator();
        if (ImGui.MenuItem("Show in Explorer"))
            ShowInExplorer(root_path);
    }

    void DrawFolderMenu(string path)
    {
        bool can_edit = !IsContentRoot(path);
        if (ImGui.MenuItem("Rename", "F2", false, can_edit))
            BeginRename(path);
        if (ImGui.MenuItem("Delete", null, false, can_edit))
            AskDelete(path, true);
        if (ImGui.BeginMenu("Change Color"))
        {
            Vector3 col = GetFolderColor(path) ?? new Vector3(0.78f, 0.64f, 0.28f);
            if (ImGui.ColorEdit3("##fc", ref col))
                SetFolderColor(path, col);
            if (ImGui.MenuItem("Reset"))
                SetFolderColor(path, null);
            ImGui.EndMenu();
        }
        ImGui.Separator();
        if (ImGui.MenuItem("Show in Explorer"))
            ShowInExplorer(path);
    }

    void DrawFileMenu(string path, EEditorFileType type)
    {
        if (ImGui.MenuItem("Rename", "F2"))
            BeginRename(path);
        if (ImGui.MenuItem("Delete"))
            AskDelete(path, false);
        if (ImGui.MenuItem("Duplicate"))
            DuplicateFile(path);
        if (type == EEditorFileType.SourceFile && ImGui.MenuItem("Create Assets"))
            EDLG_CreateAsset.Run(path);
        ImGui.Separator();
        if (ImGui.MenuItem("Show in Explorer"))
            ShowInExplorer(path);
    }

    static void ShowInExplorer(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { path = Path.GetFullPath(path); }
        catch { return; }
        bool file = File.Exists(path);
        if (!file && !Directory.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = file ? "/select,\"" + path + "\"" : "\"" + path + "\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    void NewFolder()
    {
        if (string.IsNullOrEmpty(root_path) || !Directory.Exists(root_path)) return;
        string dest = UniqueInDir(root_path, "New Folder", "");
        try { Directory.CreateDirectory(dest); }
        catch { return; }
        _selected_path = dest;
        BeginRename(dest);
    }

    void NewAsset()
    {
        if (string.IsNullOrEmpty(root_path) || !Directory.Exists(root_path)) return;
        string dir = root_path;
        EDLG_PickClass.Run(typeof(ImpAsset), t =>
        {
            if (t == null || t.IsAbstract) return;
            if (Activator.CreateInstance(t) is not ImpAsset a) return;
            string dest = UniqueInDir(dir, t.Name, a.GetFileExtension());
            WriteAsset(a, dest);
            _selected_path = dest;
        });
    }

    void NewScene()
    {
        if (string.IsNullOrEmpty(root_path) || !Directory.Exists(root_path)) return;
        string dir = root_path;
        EDLG_PickClass.Run(typeof(ImpComp), t =>
        {
            if (t == null || t.IsAbstract) return;
            if (Activator.CreateInstance(t) is not ImpComp root) return;
            A_Scene s = new();
            root.name = "Root";
            root.scene = s;
            s.root = root;
            string dest = UniqueInDir(dir, "NewScene", s.GetFileExtension());
            WriteAsset(s, dest);
            _selected_path = dest;
            on_open?.Invoke(dest);
        });
    }

    void BeginRename(string path)
    {
        if (string.IsNullOrEmpty(path) || IsContentRoot(path)) return;
        bool folder = Directory.Exists(path);
        if (!folder && !File.Exists(path)) return;
        _rename_path = path;
        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!folder && (!show_file_extensions || !IsSource(path)))
            name = Path.GetFileNameWithoutExtension(name);
        _rename_buf = name;
        _rename_focus = true;
    }

    void CommitRename(string? name)
    {
        string src = _rename_path;
        _rename_path = "";
        if (string.IsNullOrEmpty(src)) return;
        bool folder = Directory.Exists(src);
        if (!folder && !File.Exists(src)) return;
        string clean = SanitizeName(name ?? "");
        if (string.IsNullOrEmpty(clean)) return;
        string? parent = Path.GetDirectoryName(src);
        if (string.IsNullOrEmpty(parent)) return;
        if (!folder)
        {
            string ext = Path.GetExtension(src);
            if (!IsSource(src))
                clean = Path.GetFileNameWithoutExtension(clean) + ext;
            else if (string.IsNullOrEmpty(Path.GetExtension(clean)))
                clean += ext;
        }
        string dest = Path.Combine(parent, clean);
        if (PathsEqual(src, dest)) return;
        if (Directory.Exists(dest) || File.Exists(dest)) return;
        try
        {
            if (folder) Directory.Move(src, dest);
            else File.Move(src, dest);
        }
        catch { return; }
        if (folder) MoveColorCache(src, dest);
        RetargetLoaded(src, dest, folder);
        if (PathsEqual(root_path, src)) root_path = dest;
        if (PathsEqual(_selected_path, src)) _selected_path = dest;
    }

    void RetargetLoaded(string src, string dest, bool folder)
    {
        string src_abs;
        string dest_abs;
        try
        {
            src_abs = Path.GetFullPath(src);
            dest_abs = Path.GetFullPath(dest);
        }
        catch { return; }

        bool Touches(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string abs = GFile.Make_Path_Absolute(path);
            try { abs = Path.GetFullPath(abs); }
            catch { return false; }
            return PathsEqual(abs, src_abs) || (folder && IsUnder(abs, src_abs));
        }

        string MapLocal(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string abs = GFile.Make_Path_Absolute(path);
            try { abs = Path.GetFullPath(abs); }
            catch { return path; }
            string mapped = dest_abs;
            if (!PathsEqual(abs, src_abs))
            {
                if (!folder || !IsUnder(abs, src_abs)) return path;
                mapped = Path.Combine(dest_abs, Path.GetRelativePath(src_abs, abs));
            }
            try { mapped = Path.GetFullPath(mapped); }
            catch { }
            return GFile.Make_Path_Local(mapped);
        }

        void Remap<T>(Dictionary<TFile, T> map, Func<T, string> get, Action<T, string> set)
        {
            List<(TFile old, TFile neu, T val)> moves = new();
            foreach (KeyValuePair<TFile, T> kv in map)
            {
                string p = get(kv.Value);
                if (string.IsNullOrEmpty(p)) p = kv.Key.path;
                if (!Touches(p) && !Touches(kv.Key.path)) continue;
                string neu = MapLocal(p);
                set(kv.Value, neu);
                TFile nk = new(neu);
                if (kv.Key != nk)
                    moves.Add((kv.Key, nk, kv.Value));
            }
            for (int i = 0; i < moves.Count; i++)
            {
                map.Remove(moves[i].old);
                map[moves[i].neu] = moves[i].val;
            }
        }

        Remap(App.assets, a => a.filepath, (a, p) => a.filepath = p);
        Remap(App.files, f => f.filepath, (f, p) => f.filepath = p);
        foreach (ImpAsset a in App.assets.Values)
        {
            if (Touches(a.sourcefile))
                a.sourcefile = MapLocal(a.sourcefile);
        }
    }

    void AskDelete(string path, bool folder)
    {
        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string msg = folder
            ? "Delete folder '" + name + "' and all of its contents?"
            : "Delete '" + name + "'?";
        EDLG_Confirm.Run(msg, ok =>
        {
            if (ok) DeletePath(path, folder);
        });
    }

    void DeletePath(string path, bool folder)
    {
        if (string.IsNullOrEmpty(path) || IsContentRoot(path)) return;
        try
        {
            if (folder)
            {
                if (!Directory.Exists(path)) return;
                Directory.Delete(path, true);
                ForgetUnder(path);
                if (PathsEqual(root_path, path) || IsUnder(root_path, path))
                {
                    DirectoryInfo? parent = Directory.GetParent(path);
                    root_path = parent != null && IsUnder(parent.FullName, ContentRoot())
                        ? parent.FullName
                        : ContentRoot();
                }
            }
            else
            {
                if (!File.Exists(path)) return;
                File.Delete(path);
                ForgetFile(path);
            }
        }
        catch { return; }
        if (PathsEqual(_selected_path, path) || (folder && IsUnder(_selected_path, path)))
            _selected_path = "";
    }

    void DuplicateFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        string dest = UniqueCopy(path);
        try { File.Copy(path, dest); }
        catch { return; }
        _selected_path = dest;
    }

    Vector3? GetFolderColor(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return null;
        string key;
        try { key = Path.GetFullPath(dir); }
        catch { return null; }
        if (_folder_colors.TryGetValue(key, out Vector3? cached)) return cached;
        Vector3? col = null;
        string file = Path.Combine(key, ".imp_color");
        if (File.Exists(file))
        {
            try
            {
                string[] parts = File.ReadAllText(file).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                    col = new Vector3(r, g, b);
            }
            catch { }
        }
        _folder_colors[key] = col;
        return col;
    }

    void SetFolderColor(string dir, Vector3? col)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        string key;
        try { key = Path.GetFullPath(dir); }
        catch { return; }
        string file = Path.Combine(key, ".imp_color");
        try
        {
            if (col == null)
            {
                if (File.Exists(file)) File.Delete(file);
            }
            else
            {
                Vector3 c = col.Value;
                File.WriteAllText(file, string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", c.X, c.Y, c.Z));
            }
        }
        catch { }
        _folder_colors[key] = col;
    }

    void MoveColorCache(string src, string dest)
    {
        try
        {
            string s = Path.GetFullPath(src);
            string d = Path.GetFullPath(dest);
            if (_folder_colors.Remove(s, out Vector3? col))
                _folder_colors[d] = col;
        }
        catch { }
    }

    void DrawRenameField(float width)
    {
        if (_rename_focus)
        {
            ImGui.SetKeyboardFocusHere();
            _rename_focus = false;
        }
        ImGui.SetNextItemWidth(width);
        bool enter = ImGui.InputText("##fb_ren", ref _rename_buf, 256,
            ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
        if (enter)
            CommitRename(_rename_buf);
        else if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                _rename_path = "";
            else
                CommitRename(_rename_buf);
        }
    }

    static void WriteAsset(ImpAsset asset, string abs)
    {
        string? dir = Path.GetDirectoryName(abs);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        string local = GFile.Make_Path_Local(abs);
        asset.filepath = local;
        asset.is_inlined = false;
        App.assets[new TFile(local)] = asset;
        asset.Save(true);
    }

    static void ForgetFile(string abs)
    {
        string local = GFile.Make_Path_Local(abs);
        App.assets.Remove(new TFile(local));
        App.files.Remove(new TFile(local));
    }

    static void ForgetUnder(string dir)
    {
        string prefix;
        try { prefix = Path.GetFullPath(dir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar; }
        catch { return; }

        void Sweep<T>(Dictionary<TFile, T> map)
        {
            List<TFile> drop = new();
            foreach (TFile key in map.Keys)
            {
                string abs = GFile.Make_Path_Absolute(key.path);
                try
                {
                    if (Path.GetFullPath(abs).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        drop.Add(key);
                }
                catch { }
            }
            for (int i = 0; i < drop.Count; i++)
                map.Remove(drop[i]);
        }

        Sweep(App.assets);
        Sweep(App.files);
    }

    bool IsContentRoot(string path) => PathsEqual(path, ContentRoot());

    bool NameMatch(string path)
    {
        if (string.IsNullOrWhiteSpace(filter_string)) return true;
        return Path.GetFileName(path).Contains(filter_string, StringComparison.OrdinalIgnoreCase);
    }

    static string UniqueInDir(string dir, string name, string ext)
    {
        string dest = Path.Combine(dir, name + ext);
        if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
        int n = 1;
        while (true)
        {
            dest = Path.Combine(dir, name + n + ext);
            if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
            n++;
        }
    }

    static string UniqueCopy(string path)
    {
        string dir = Path.GetDirectoryName(path) ?? "";
        string ext = Path.GetExtension(path);
        string stem = Path.GetFileNameWithoutExtension(path);
        int cut = stem.Length;
        while (cut > 0 && char.IsDigit(stem[cut - 1])) cut--;
        string base_stem = cut > 0 ? stem[..cut] : stem;
        int n = 1;
        if (cut < stem.Length && int.TryParse(stem[cut..], out int existing))
            n = existing + 1;
        while (true)
        {
            string dest = Path.Combine(dir, base_stem + n + ext);
            if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
            n++;
        }
    }

    static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim();
        if (name is "." or "..") return "";
        return name;
    }

    static bool IsHidden(string path)
    {
        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) || name.StartsWith('.');
    }

    static bool IsSource(string file)
    {
        string ext = Path.GetExtension(file);
        return !ext.Equals(".ImpAsset", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".ImpScene", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".ImpGame", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".ImpMod", StringComparison.OrdinalIgnoreCase);
    }

    static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd('\\', '/'),
                Path.GetFullPath(b).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;
        try
        {
            string p = Path.GetFullPath(path).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string r = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return p.StartsWith(r, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    static string RelPath(string path, string root)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return "";
        try
        {
            string rel = Path.GetRelativePath(root, path);
            if (rel == "." || rel.StartsWith("..")) return "";
            return rel.Replace('\\', '/');
        }
        catch { return ""; }
    }
}

public enum EEditorFileType
{
    Asset, SourceFile,
}

public class EUI_FileThumbnail : EdUi
{
    public bool is_folder;
    public EEditorFileType type;
    public bool is_selected;
    public string path = "";
    public string? label_override;
    public float size = 50;
    public bool show_ext = true;
    public Vector4? color_override;
    public bool renaming;
    public string rename_buf = "";
    public bool rename_focus;
    public Action<EUI_FileThumbnail>? on_click;
    public Action<EUI_FileThumbnail>? on_double_click;
    public Action<EUI_FileThumbnail>? on_popup;
    public Action<string>? on_rename;
    public Action? on_rename_cancel;

    static readonly Dictionary<string, Texture2D> _thumbs = new(StringComparer.OrdinalIgnoreCase);
    static Texture2D _ico_folder;
    static Texture2D _ico_file;
    static Texture2D _ico_asset;
    static bool _icos_tried;

    public override void OnDraw()
    {
        ImGui.PushID(path ?? "");
        ImGui.BeginGroup();

        Vector2 sz = new(size, size);
        if (is_selected)
            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
        ImGui.Selectable("##s", is_selected, ImGuiSelectableFlags.AllowDoubleClick, sz);
        if (is_selected) ImGui.PopStyleColor();

        if (ImGui.IsItemClicked() || ImGui.IsItemClicked(ImGuiMouseButton.Right))
            on_click?.Invoke(this);
        if (!renaming && ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            on_double_click?.Invoke(this);

        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        uint fill = ColorOf();
        dl.AddRectFilled(min, max, fill, 3f);

        Texture2D tex = Preview();
        if (tex.Id != 0)
        {
            Vector2 imin = min + new Vector2(3f, 3f);
            Vector2 imax = max - new Vector2(3f, 3f);
            Fit(tex.Width, tex.Height, ref imin, ref imax);
            dl.AddImage((IntPtr)tex.Id, imin, imax, new Vector2(0f, 1f), new Vector2(1f, 0f));
        }

        if (is_selected)
            dl.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.HeaderActive), 3f, 0, 2f);

        if (label_override != ".." && ImGui.BeginPopupContextItem("##fb_ctx"))
        {
            on_popup?.Invoke(this);
            ImGui.EndPopup();
        }

        if (renaming)
        {
            if (rename_focus)
                ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(size);
            bool enter = ImGui.InputText("##ren", ref rename_buf, 256,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
            if (enter)
                on_rename?.Invoke(rename_buf);
            else if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                    on_rename_cancel?.Invoke();
                else
                    on_rename?.Invoke(rename_buf);
            }
        }
        else
        {
            string name = label_override ?? DisplayName();
            if (name.Length > 18) name = name[..16] + "..";
            ImGui.SetWindowFontScale(0.85f);
            ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + size);
            ImGui.TextUnformatted(name);
            ImGui.PopTextWrapPos();
            ImGui.SetWindowFontScale(1f);
        }

        ImGui.EndGroup();
        ImGui.PopID();
    }

    public override List<TEdPopupOption> Popup_GetOptions()
    {
        return new List<TEdPopupOption>
        {
            new() { text = "Open", on_select = () => on_double_click?.Invoke(this) },
            new() { text = "Copy Path", on_select = () => ImGui.SetClipboardText(path ?? "") },
        };
    }

    string DisplayName()
    {
        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (is_folder || show_ext) return name;
        return Path.GetFileNameWithoutExtension(name);
    }

    uint ColorOf()
    {
        if (color_override != null)
            return ImGui.ColorConvertFloat4ToU32(color_override.Value);
        if (is_folder) return ImGui.ColorConvertFloat4ToU32(new Vector4(0.78f, 0.64f, 0.28f, 0.55f));
        if (type == EEditorFileType.Asset) return ImGui.ColorConvertFloat4ToU32(new Vector4(0.28f, 0.62f, 0.38f, 0.55f));
        return ImGui.ColorConvertFloat4ToU32(new Vector4(0.38f, 0.42f, 0.50f, 0.55f));
    }

    Texture2D Preview()
    {
        EnsureIcons();
        if (is_folder) return _ico_folder;
        string ext = Path.GetExtension(path);
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            if (_thumbs.TryGetValue(path, out Texture2D cached)) return cached;
            Texture2D loaded = LoadThumb(path);
            _thumbs[path] = loaded;
            if (loaded.Id != 0) return loaded;
        }
        if (type == EEditorFileType.Asset) return _ico_asset;
        return _ico_file;
    }

    static void EnsureIcons()
    {
        if (_icos_tried) return;
        _icos_tried = true;
        _ico_folder = LoadIcon("{engine}Editor/Icons/_folder.png");
        _ico_file = LoadIcon("{engine}Editor/Icons/_file.png");
        _ico_asset = LoadIcon("{engine}Editor/Icons/ImpAsset.png");
        if (_ico_asset.Id == 0)
            _ico_asset = LoadIcon("{engine}Editor/Types/ImpAsset.png");
    }

    static Texture2D LoadIcon(string local)
    {
        string abs = GFile.Make_Path_Absolute(local);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) return default;
        try { return Raylib.LoadTexture(abs); }
        catch { return default; }
    }

    static Texture2D LoadThumb(string file)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return default;
        try
        {
            Image img = Raylib.LoadImage(file);
            if (img.Width <= 0 || img.Height <= 0)
            {
                Raylib.UnloadImage(img);
                return default;
            }
            const int maxd = 128;
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
}
