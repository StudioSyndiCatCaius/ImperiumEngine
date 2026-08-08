using System.Numerics;
using Editor.Dialog;
using ImGuiNET;
using ImperiumEngine.Classes;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Objects.Assets;
using Raylib_cs;

namespace Editor.Windows;

//UE-style content browser: folder tree on the left (Game/Editor tabs), tile grid on the right
public class WND_FileExplorer : EditorWindow
{
    enum ERoot { Game, Editor }

    ERoot _root = ERoot.Game;
    string _rel_path = "";  //current folder, relative to the active root's content dir
    string _selected = "";  //full path of the selected tile

    float _tree_width = 220f;
    float _tile_size = 96f;

    //cached listing of the current folder, refreshed on navigation or every second
    string _cache_key = "\0";
    double _cache_age;
    string[] _cache_dirs = [];
    string[] _cache_files = [];

    // inline folder rename: when set, the matching tile shows an InputText instead of its label
    string _rename_path = "";
    string _rename_buf = "";
    bool _rename_focus_pending;   // request focus on the input the frame rename starts
    string _rename_error = "";

    //shared across all explorer windows: default thumbnails + extension -> asset prototype
    static bool s_statics_loaded;
    static Texture2D s_thumb_folder;
    static Texture2D s_thumb_doc;
    static readonly Dictionary<string, I_EditorAsset> s_asset_types = new(StringComparer.OrdinalIgnoreCase);

    public override string Title => "File Explorer";

    string RootDir => _root == ERoot.Game
        ? Path.Combine(ImpFile.s_projectDir, "Content")
        : ImpFile.s_engineContentDir;

    string CurrentDir => Path.Combine(RootDir, _rel_path);

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        EnsureStatics();
        _cache_age += delta;

        DrawPathBar();
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        _tree_width = Math.Clamp(_tree_width, 120f, Math.Max(120f, avail.X - 200f));

        // --- folder tree (left) ---
        ImGui.BeginChild("tree_pane", new Vector2(_tree_width, 0), ImGuiChildFlags.Borders);
        DrawTreePane();
        ImGui.EndChild();

        ImGui.SameLine();
        SplitterVertical("tree_splitter", 4f, ref _tree_width);
        ImGui.SameLine();

        // --- tile grid (right) ---
        ImGui.BeginChild("tile_pane", new Vector2(0, 0));
        {
            float footer_h = ImGui.GetFrameHeightWithSpacing();
            ImGui.BeginChild("tiles", new Vector2(0, -footer_h));
            DrawTileGrid();
            ImGui.EndChild();
            DrawFooter();
        }
        ImGui.EndChild();
    }

    //loads the default thumbnails and builds the extension -> asset prototype registry
    static void EnsureStatics()
    {
        if (s_statics_loaded) return;
        s_statics_loaded = true;

        s_thumb_folder = LoadThumb("{engine}/2D/Thumbnails/T_thumb_folder.png");
        s_thumb_doc = LoadThumb("{engine}/2D/Thumbnails/T_thumb_doc.png");

        foreach (var type in typeof(ImpAsset).Assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(ImpAsset)) || type.IsAbstract) continue;
            if (type.GetConstructor(Type.EmptyTypes) == null) continue;
            var proto = (ImpAsset)Activator.CreateInstance(type)!;
            s_asset_types.TryAdd(proto.GetExtension(), proto);
        }
    }

    static Texture2D LoadThumb(string path)
    {
        var tex = Raylib.LoadTexture(ImpFile.Path_ToAbsolute(path));
        if (tex.Id != 0) Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
        return tex;
    }

    // --- top bar: clickable breadcrumbs for the current path ---
    void DrawPathBar()
    {
        if (ImGui.Button("Content")) Navigate("");

        string[] segments = SplitPath(_rel_path);
        string walk = "";
        foreach (string seg in segments)
        {
            walk = Path.Combine(walk, seg);
            ImGui.SameLine(0, 4);
            ImGui.TextDisabled(">");
            ImGui.SameLine(0, 4);
            string target = walk; //capture before the loop advances
            if (ImGui.Button(seg)) Navigate(target);
        }
    }

    // --- left pane: Game/Editor tabs, each with a recursive folder tree ---
    void DrawTreePane()
    {
        if (!ImGui.BeginTabBar("root_tabs")) return;

        if (ImGui.BeginTabItem("Game"))
        {
            SetRoot(ERoot.Game);
            DrawTreeRoot();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Editor"))
        {
            SetRoot(ERoot.Editor);
            DrawTreeRoot();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    void SetRoot(ERoot root)
    {
        if (_root == root) return;
        _root = root;
        Navigate("");
    }

    void DrawTreeRoot()
    {
        if (!Directory.Exists(RootDir))
        {
            ImGui.TextDisabled("(content dir not found)");
            return;
        }

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth |
                    ImGuiTreeNodeFlags.DefaultOpen;
        if (_rel_path == "") flags |= ImGuiTreeNodeFlags.Selected;

        bool open = ImGui.TreeNodeEx("Content", flags);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Navigate("");
        if (!open) return;

        foreach (string dir in SafeGetDirs(RootDir))
            DrawTreeNode(dir, Path.GetFileName(dir));
        ImGui.TreePop();
    }

    void DrawTreeNode(string dir, string rel)
    {
        string[] subdirs = SafeGetDirs(dir);

        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                    ImGuiTreeNodeFlags.SpanAvailWidth;
        if (subdirs.Length == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (rel == _rel_path) flags |= ImGuiTreeNodeFlags.Selected;

        bool open = ImGui.TreeNodeEx(Path.GetFileName(dir), flags);
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Navigate(rel);
        if (!open) return;

        foreach (string sub in subdirs)
            DrawTreeNode(sub, Path.Combine(rel, Path.GetFileName(sub)));
        ImGui.TreePop();
    }

    // --- right pane: tile grid, folders first ---
    void DrawTileGrid()
    {
        RefreshCache();

        if (_cache_dirs.Length == 0 && _cache_files.Length == 0)
            ImGui.TextDisabled("(empty)");

        float label_h = ImGui.GetTextLineHeight() * 2f + 6f;
        float tile_h = _tile_size + label_h;
        float cell_w = _tile_size + ImGui.GetStyle().ItemSpacing.X;
        int columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / cell_w));

        int i = 0;
        foreach (string dir in _cache_dirs)
        {
            if (i++ % columns != 0) ImGui.SameLine();
            DrawTile(dir, tile_h, is_dir: true);
        }
        foreach (string file in _cache_files)
        {
            if (i++ % columns != 0) ImGui.SameLine();
            DrawTile(file, tile_h, is_dir: false);
        }

        //right-click empty space (not on a tile): create menu for the current folder
        if (ImGui.BeginPopupContextWindow("empty_ctx", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            DrawEmptyContextMenu();
            ImGui.EndPopup();
        }
    }

    void DrawFolderContextMenu(string path)
    {
        if (ImGui.MenuItem("Rename"))
            BeginRename(path);
        if (ImGui.MenuItem("Delete"))
            RequestDeleteFolder(path);
        ImGui.MenuItem("Set Color");
    }

    void DrawFileContextMenu(string path)
    {
        // proxy-specific actions first (e.g. a .png's "Create Texture2D"), then the generic ones
        var proxy = EditorFileProxy.For(path);
        if (proxy != null)
        {
            proxy.OnContextMenu(ProxyContext(path));
            ImGui.Separator();
        }

        ImGui.MenuItem("Copy");
        ImGui.MenuItem("Rename");
        ImGui.MenuItem("Delete");
    }

    // context a file proxy acts through: the file plus the create-and-select hook
    FileProxyContext ProxyContext(string path) => new()
    {
        FilePath = path,
        OnFileCreated = OnItemCreated,
    };

    // true if the extension maps to an engine asset type (so double-click opens the asset editor);
    // raw source files (.png, .glb...) return false and defer to their proxy instead.
    static bool IsAssetFile(string path) => s_asset_types.ContainsKey(Path.GetExtension(path));

    void DrawEmptyContextMenu()
    {
        if (ImGui.MenuItem("New Folder"))
            CreateNewFolder();
        if (ImGui.MenuItem("New Asset"))
            DLG_NewAsset.Show(CurrentDir, OnItemCreated);
        if (ImGui.MenuItem("New Entity"))
            DLG_NewEntity.Show(CurrentDir, OnItemCreated);
        if (ImGui.MenuItem("New Level"))
            DLG_NewAsset.ShowFixed(CurrentDir, typeof(A_Level), "New Level", OnItemCreated);
        ImGui.BeginDisabled();   // scripting system not built yet
        ImGui.MenuItem("New Script");
        ImGui.EndDisabled();
    }

    // a dialog created a file in the current folder: refresh the listing and select it
    void OnItemCreated(string full)
    {
        _cache_key = "\0";   // force a listing refresh next frame
        _selected = full;
    }

    // Creates a subfolder in the current directory with a unique default name, then selects it
    // and enters inline rename so the user can immediately type a real name.
    void CreateNewFolder()
    {
        string name = "New Folder";
        for (int n = 1; Directory.Exists(Path.Combine(CurrentDir, name)); n++)
            name = $"New Folder {n}";

        string full = Path.Combine(CurrentDir, name);
        try { Directory.CreateDirectory(full); }
        catch (Exception ex) { Console.WriteLine($"[FileExplorer] Could not create folder: {ex.Message}"); return; }

        OnItemCreated(full);
        BeginRename(full);
    }

    // ---------------------------------------------------------------------------------------------
    // Folder rename / delete
    // ---------------------------------------------------------------------------------------------

    void BeginRename(string path)
    {
        _rename_path = path;
        _rename_buf = Path.GetFileName(path);
        _rename_focus_pending = true;
        _rename_error = "";
        _selected = path;
    }

    void CancelRename()
    {
        _rename_path = "";
        _rename_buf = "";
        _rename_error = "";
        _rename_focus_pending = false;
    }

    // Commits the inline rename. Returns true if the folder was renamed (or the name was unchanged).
    bool CommitRename()
    {
        if (_rename_path == "") return true;

        string old_path = _rename_path;
        string new_name = _rename_buf.Trim();
        _rename_error = "";

        if (new_name.Length == 0)
        {
            _rename_error = "Name cannot be empty.";
            return false;
        }
        if (new_name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            _rename_error = "Invalid characters in name.";
            return false;
        }

        string parent = Path.GetDirectoryName(old_path) ?? "";
        string new_path = Path.Combine(parent, new_name);

        // same name (possibly different casing on Windows) — treat as no-op
        if (string.Equals(old_path, new_path, StringComparison.OrdinalIgnoreCase))
        {
            // Windows allows case-only renames via a two-step move; skip for now
            CancelRename();
            return true;
        }

        if (Directory.Exists(new_path) || File.Exists(new_path))
        {
            _rename_error = "A file or folder with that name already exists.";
            return false;
        }

        try { Directory.Move(old_path, new_path); }
        catch (Exception ex)
        {
            _rename_error = ex.Message;
            Console.WriteLine($"[FileExplorer] Rename failed: {ex.Message}");
            return false;
        }

        // rewrite on-disk references ({game}/Old/… → {game}/New/…) then fix open editor docs
        int rewritten = ImpFile.RewritePathReferences(old_path, new_path);
        if (rewritten > 0)
            Console.WriteLine($"[FileExplorer] Updated references in {rewritten} file(s).");
        NotifyPathsRemapped(old_path, new_path);

        // keep navigation/selection coherent when the renamed folder is the current view or selection
        RemapExplorerState(old_path, new_path);

        CancelRename();
        _cache_key = "\0";
        return true;
    }

    // Empty folders delete immediately; non-empty folders open a confirmation that lists every
    // file about to be removed.
    void RequestDeleteFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        bool empty;
        try
        {
            empty = !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileExplorer] Could not inspect folder: {ex.Message}");
            return;
        }

        if (empty)
            DeleteFolder(path);
        else
            DLG_DeleteFolder.Ask(path, () => DeleteFolder(path));
    }

    void DeleteFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        // drop open asset tabs that live inside the folder before the files vanish
        NotifyPathsDeleted(path);

        try { Directory.Delete(path, recursive: true); }
        catch (Exception ex)
        {
            Console.WriteLine($"[FileExplorer] Delete failed: {ex.Message}");
            return;
        }

        // if we were browsing inside the deleted tree, pop back to its parent
        try
        {
            string rel = Path.GetRelativePath(RootDir, path);
            if (rel != "." && !rel.StartsWith(".."))
            {
                string rel_norm = rel.Replace('\\', '/');
                string cur_norm = _rel_path.Replace('\\', '/');
                if (cur_norm == rel_norm || cur_norm.StartsWith(rel_norm + "/", StringComparison.OrdinalIgnoreCase))
                {
                    string parent_rel = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
                    if (parent_rel == ".") parent_rel = "";
                    Navigate(parent_rel);
                }
            }
        }
        catch { /* RootDir comparison failed — just refresh */ }

        if (string.Equals(_selected, path, StringComparison.OrdinalIgnoreCase) ||
            ImpFile.Path_IsUnder(_selected, path))
            _selected = "";

        if (string.Equals(_rename_path, path, StringComparison.OrdinalIgnoreCase))
            CancelRename();

        _cache_key = "\0";
    }

    // Updates this explorer's selection / current path after a folder rename.
    void RemapExplorerState(string old_path, string new_path)
    {
        if (ImpFile.Path_IsUnder(_selected, old_path) ||
            string.Equals(_selected, old_path, StringComparison.OrdinalIgnoreCase))
            _selected = ImpFile.Path_Remap(_selected, old_path, new_path);

        try
        {
            string old_rel = Path.GetRelativePath(RootDir, old_path).Replace('\\', '/');
            string new_rel = Path.GetRelativePath(RootDir, new_path).Replace('\\', '/');
            if (old_rel == "." || old_rel.StartsWith("..")) return;

            string cur = _rel_path.Replace('\\', '/');
            if (cur == old_rel)
                _rel_path = new_rel == "." ? "" : new_rel;
            else if (cur.StartsWith(old_rel + "/", StringComparison.OrdinalIgnoreCase))
                _rel_path = (new_rel == "." ? "" : new_rel) + cur[old_rel.Length..];
        }
        catch { /* keep current path */ }
    }

    // Updates open level / asset documents and editor session paths after a content rename.
    static void NotifyPathsRemapped(string old_dir, string new_dir)
    {
        foreach (var w in Program.windows_open)
        {
            if (w is WND_AssetEdit asset_edit)
                asset_edit.RemapPaths(old_dir, new_dir);
            else if (w.DocumentAsset != null)
                ImpFile.RemapAssetPathsInMemory(w.DocumentAsset, old_dir, new_dir);
        }

        // EditorConfig.last_level is keyword-relative — rewrite if it pointed into the folder
        if (Program.config != null && Program.config.last_level != "")
        {
            string abs = ImpFile.Path_ToAbsolute(Program.config.last_level);
            if (ImpFile.Path_IsUnder(abs, old_dir))
                Program.config.last_level = ImpFile.Path_ToRelative(
                    ImpFile.Path_Remap(abs, old_dir, new_dir));
        }
    }

    // Closes open asset tabs that lived under a deleted folder; remaps are unnecessary since the
    // files are gone. The level editor keeps its in-memory document even if its file was deleted.
    static void NotifyPathsDeleted(string dir)
    {
        foreach (var w in Program.windows_open)
        {
            if (w is WND_AssetEdit asset_edit)
                asset_edit.CloseTabsUnder(dir);
        }
    }

    void DrawTile(string path, float tile_h, bool is_dir)
    {
        string name = Path.GetFileName(path);
        ImGui.PushID(path);

        bool renaming = is_dir && string.Equals(_rename_path, path, StringComparison.OrdinalIgnoreCase);

        var pos = ImGui.GetCursorScreenPos();
        if (ImGui.Selectable("##tile", _selected == path,
                ImGuiSelectableFlags.AllowDoubleClick, new Vector2(_tile_size, tile_h)))
            _selected = path;

        if (!renaming && ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            if (is_dir) Navigate(Path.Combine(_rel_path, name));
            else if (IsAssetFile(path)) WND_AssetEdit.OpenAsset(path);   // .impasset etc → asset editor
            else EditorFileProxy.For(path)?.OnOpen(ProxyContext(path));  // raw files: proxy decides (default: nothing)
        }

        //files are draggable onto inspector asset slots (creates a reference to this file)
        if (!is_dir && ImGui.BeginDragDropSource())
        {
            EditorDragDrop.SetAsset(path);
            ImGui.TextUnformatted(name);
            ImGui.EndDragDropSource();
        }

        //right-click a tile: select it, then show the folder/asset context menu
        if (!renaming && ImGui.BeginPopupContextItem("tile_ctx"))
        {
            _selected = path;
            if (is_dir) DrawFolderContextMenu(path);
            else DrawFileContextMenu(path);
            ImGui.EndPopup();
        }

        //resolve thumbnail: folders always use the folder thumb; assets ask their
        //I_EditorAsset prototype (by extension), falling back to the doc thumb + white
        Texture2D tex = s_thumb_folder;
        Vector4 tint = new(1, 1, 1, 1);
        if (!is_dir)
        {
            s_asset_types.TryGetValue(Path.GetExtension(name), out var proto);
            var custom = proto?.GetThumbnailTexture() ?? default;
            tex = custom.Id != 0 ? custom : s_thumb_doc;
            var c = proto?.GetThumbnailColor() ?? Color.White;
            tint = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }

        var dl = ImGui.GetWindowDrawList();
        float pad = 8f;
        if (tex.Id != 0)
            dl.AddImage((IntPtr)tex.Id,
                pos + new Vector2(pad, pad),
                pos + new Vector2(_tile_size - pad, _tile_size - pad),
                Vector2.Zero, Vector2.One, ImGui.GetColorU32(tint));

        // name under the icon — inline InputText while renaming a folder
        if (renaming)
        {
            ImGui.SetCursorScreenPos(new Vector2(pos.X + 2f, pos.Y + _tile_size));
            ImGui.SetNextItemWidth(_tile_size - 4f);
            if (_rename_focus_pending)
            {
                ImGui.SetKeyboardFocusHere();
                _rename_focus_pending = false;
            }

            var flags = ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue;
            bool enter = ImGui.InputText("##rename", ref _rename_buf, 128, flags);
            if (enter || ImGui.IsItemDeactivatedAfterEdit())
            {
                // Enter or click-away after editing — keep the field open (and re-focus) on error
                if (!CommitRename() && _rename_path != "")
                    _rename_focus_pending = true;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.Escape, false) || ImGui.IsItemDeactivated())
                CancelRename();

            if (_rename_error != "" && _rename_path != "")
            {
                // failed commit: re-open rename next frame with the error visible under the tile
                var err_pos = new Vector2(pos.X, pos.Y + _tile_size + ImGui.GetFrameHeight());
                dl.AddText(err_pos, ImGui.GetColorU32(new Vector4(1f, 0.4f, 0.4f, 1f)),
                    Ellipsize(_rename_error, _tile_size));
            }
        }
        else
        {
            string label = Ellipsize(is_dir ? name : Path.GetFileNameWithoutExtension(name), _tile_size - 4f);
            float label_w = ImGui.CalcTextSize(label).X;
            dl.AddText(new Vector2(pos.X + (_tile_size - label_w) * 0.5f, pos.Y + _tile_size),
                ImGui.GetColorU32(ImGuiCol.Text), label);

            if (!is_dir)
            {
                string ext = Ellipsize(Path.GetExtension(name).TrimStart('.'), _tile_size - 4f);
                float ext_w = ImGui.CalcTextSize(ext).X;
                dl.AddText(new Vector2(pos.X + (_tile_size - ext_w) * 0.5f, pos.Y + _tile_size + ImGui.GetTextLineHeight()),
                    ImGui.GetColorU32(ImGuiCol.TextDisabled), ext);
            }
        }

        ImGui.PopID();
    }

    void DrawFooter()
    {
        ImGui.TextDisabled($"{_cache_dirs.Length + _cache_files.Length} items");
        ImGui.SameLine(Math.Max(0, ImGui.GetContentRegionAvail().X - 110f));
        ImGui.SetNextItemWidth(110f);
        ImGui.SliderFloat("##tile_size", ref _tile_size, 48f, 160f, "");
    }

    void Navigate(string rel)
    {
        if (_rename_path != "") CancelRename();
        _rel_path = rel;
        _selected = "";
        _cache_key = "\0"; //force a listing refresh
    }

    void RefreshCache()
    {
        string key = CurrentDir;
        if (key == _cache_key && _cache_age < 1.0) return;
        _cache_key = key;
        _cache_age = 0;

        //if the folder vanished (deleted externally), fall back to the root
        if (!Directory.Exists(key) && _rel_path != "")
        {
            Navigate("");
            key = CurrentDir;
            _cache_key = key;
        }

        _cache_dirs = SafeGetDirs(key);
        _cache_files = SafeGetFiles(key);
    }

    static string[] SafeGetDirs(string dir)
    {
        try { return Directory.GetDirectories(dir).OrderBy(Path.GetFileName).ToArray(); }
        catch { return []; }
    }

    static string[] SafeGetFiles(string dir)
    {
        try { return Directory.GetFiles(dir).OrderBy(Path.GetFileName).ToArray(); }
        catch { return []; }
    }

    static string[] SplitPath(string rel) =>
        rel.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

    static string Ellipsize(string s, float max_w)
    {
        if (ImGui.CalcTextSize(s).X <= max_w) return s;
        while (s.Length > 1 && ImGui.CalcTextSize(s + "..").X > max_w) s = s[..^1];
        return s + "..";
    }

    //draggable vertical bar — dragging resizes the tree pane
    static void SplitterVertical(string id, float thickness, ref float width)
    {
        ImGui.InvisibleButton(id, new Vector2(thickness, -1));
        if (ImGui.IsItemActive())
            width += ImGui.GetIO().MouseDelta.X;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
    }
}
