using System.Numerics;
using System.Text.RegularExpressions;
using Editor.Dialog;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Panel;

public class PNL_FileBrowser : EdPanel
{
    [ImpVar] public bool show_source_files = true;
    [ImpVar] public bool show_file_extensions = false;
    [ImpVar] public bool show_engine_content = true;
    [ImpVar(Min = 16, Max = 32)] public float row_height = 22;

    C2_List _root = new()
    {
        orentation = EUIOrentation.V,
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
        spacing = 0,
    };

    C2_SearchBar _search = new()
    {
        placeholder = "Search Assets",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
        },
    };

    C2_TabBox tab_dir = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
        tab_height = 24,
        tab_width = 90,
    };

    EdFileTree tree_dir_game = new()
    {
        name = "Game",
        root_path = ImpFile.ContentDir_Game(),
    };

    EdFileTree tree_dir_engine = new()
    {
        name = "Engine",
        root_path = ImpFile.ContentDir_Engine(),
    };

    C2_Expandable _settings = new()
    {
        name = "Settings",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
        },
        bar_height = 22,
        is_expanded = false,
    };

    C2_Inspector settings_inspector = new()
    {
        name = "Settings",
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            size = new Vector2(0, 120),
            size_min = new Vector2(0, 120),
        },
        declared_only = true,
        use_categories = false,
        show_header = false,
        show_search = false,
    };

    string _current_dir = "";
    public string CurrentDir => _current_dir;
    string _query = "";
    string _selected_path = "";
    static readonly List<PNL_FileBrowser> _browsers = new();

    bool _last_src = true;
    bool _last_ext;
    bool _last_engine = true;
    float _last_row_height = 22;

    public PNL_FileBrowser()
    {
        name = "Content Browser";
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;
        clip_children = true;
        style = new UI_Box { tint = new Color(36, 36, 36, 255) };

        Child_Add(_root);
        _root.Child_Add(_search);
        _root.Child_Add(tab_dir);
        _root.Child_Add(_settings);

        _search.on_search = q =>
        {
            _query = q ?? "";
            Query_Apply();
        };

        tab_dir.Child_Add(tree_dir_game);
        if (show_engine_content) tab_dir.Child_Add(tree_dir_engine);
        Tree_Wire(tree_dir_game);
        Tree_Wire(tree_dir_engine);
        tab_dir.on_tab_change = i =>
        {
            EdFileTree tree = i == 0 ? tree_dir_game : tree_dir_engine;
            string path = tree.selected_data is TDirectory d && !string.IsNullOrEmpty(d.path)
                ? d.path
                : tree.root_path;
            CurrentDir_Set(new TDirectory { path = path });
        };

        _settings.Child_Add(settings_inspector);
        settings_inspector.Object_Add(this, true);

        tree_dir_game.root_path = ImpFile.ContentDir_Game();
        tree_dir_engine.root_path = ImpFile.ContentDir_Engine();
        string start = tree_dir_game.root_path;
        if (!Directory.Exists(start)) start = tree_dir_engine.root_path;
        CurrentDir_Set(new TDirectory { path = start });
        _browsers.Add(this);
    }

    void Tree_Wire(EdFileTree tree)
    {
        tree.on_item_click = _Item_Select;
        tree.on_item_double_click = _Item_Open;
        tree.on_item_right_click = _Item_RightClick;
        tree.on_item_drop_external = _Item_DropExternal;
        tree.item_drag_payload = _Item_DragPayload;
        tree.on_background_right_click = () =>
        {
            Vector2 pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
            ImpPlayer.Popup_Run(this, new A_PopupConfig { options = EmptyFolderOptions() }, null, pos);
        };
        tree.on_rebuilt = SyncTreeSelection;
        tree.show_source_files = show_source_files;
        tree.show_file_extensions = show_file_extensions;
        tree.row_height = row_height;
    }

    /// <summary>
    /// Every open file browser after a disk change. Pass from (and to, if it moved)
    /// so current dirs follow the path instead of going stale.
    /// </summary>
    public static void Browsers_Notify(string from = null, string to = null)
    {
        for (int i = _browsers.Count - 1; i >= 0; i--)
        {
            if (_browsers[i] == null || (_browsers[i].parent == null && !_browsers[i].is_visible))
            {
                _browsers.RemoveAt(i);
            }
        }
        for (int i = 0; i < _browsers.Count; i++)
        {
            _browsers[i].Disk_OnChanged(from, to);
        }
    }

    void Disk_OnChanged(string from, string to)
    {
        if (!string.IsNullOrEmpty(from))
        {
            if (string.IsNullOrEmpty(to))
            {
                if (PathsEqual(_selected_path, from) || IsUnder(_selected_path, from))
                {
                    _selected_path = "";
                }
                if (PathsEqual(_current_dir, from) || IsUnder(_current_dir, from))
                {
                    string parent = Path.GetDirectoryName(from);
                    if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                    {
                        parent = tree_dir_game.root_path;
                    }
                    CurrentDir_Set(new TDirectory { path = parent });
                }
            }
            else
            {
                _selected_path = Path_Moved(_selected_path, from, to);
                string moved_dir = Path_Moved(_current_dir, from, to);
                if (!string.Equals(moved_dir, _current_dir, StringComparison.Ordinal))
                {
                    CurrentDir_Set(new TDirectory { path = moved_dir });
                }
            }
        }

        if (!string.IsNullOrEmpty(_current_dir) && !Directory.Exists(_current_dir))
        {
            string walk = _current_dir;
            while (!string.IsNullOrEmpty(walk) && !Directory.Exists(walk))
            {
                walk = Path.GetDirectoryName(walk);
            }
            if (string.IsNullOrEmpty(walk) || !Directory.Exists(walk))
            {
                walk = tree_dir_game.root_path;
            }
            CurrentDir_Set(new TDirectory { path = walk });
        }

        RefreshAll();
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Drag_Sync();

        string game_root = ImpFile.ContentDir_Game();
        string engine_root = ImpFile.ContentDir_Engine();
        if (Directory.Exists(game_root)
            && !PathsEqual(game_root, engine_root)
            && !IsUnder(game_root, engine_root)
            && !string.Equals(tree_dir_game.root_path, game_root, StringComparison.OrdinalIgnoreCase))
        {
            tree_dir_game.root_path = game_root;
        }
        if (!string.Equals(tree_dir_engine.root_path, engine_root, StringComparison.OrdinalIgnoreCase))
        {
            tree_dir_engine.root_path = engine_root;
        }

        if (_last_engine != show_engine_content)
        {
            _last_engine = show_engine_content;
            ApplyEngineContent();
        }

        if (_last_src != show_source_files || _last_ext != show_file_extensions
            || MathF.Abs(_last_row_height - row_height) > 0.25f)
        {
            _last_src = show_source_files;
            _last_ext = show_file_extensions;
            _last_row_height = row_height;
            Tree_Wire(tree_dir_game);
            Tree_Wire(tree_dir_engine);
            RefreshAll();
        }

        Keys_Poll();
    }

    // F2 / Delete / Ctrl+D on whatever row was last clicked. The tree rows live in the engine
    // assembly and have no idea what a file is, so the shortcuts sit here instead.
    void Keys_Poll()
    {
        if (EdRenameField.IsOpen || string.IsNullOrEmpty(_selected_path) || !Focus_IsOurs()) return;

        if (ImpPlayer.Key_IsPressed(EInputKey.Key_F2))
        {
            Rename_Begin(_selected_path);
        }
        else if (ImpPlayer.Key_IsPressed(EInputKey.Key_Delete))
        {
            Path_DeleteAsk(_selected_path);
        }
        else if (ImpPlayer.Key_IsPressed(EInputKey.Key_D)
                 && (ImpPlayer.Key_IsDown(EInputKey.Key_LeftControl) || ImpPlayer.Key_IsDown(EInputKey.Key_RightControl)))
        {
            Path_Duplicate(_selected_path);
        }
    }

    // "The browser has the keyboard": the last thing clicked was inside this panel, and it was not
    // a text field that wants the keys for itself.
    bool Focus_IsOurs()
    {
        if (ImpPlayer.players.Count == 0 || !IsVisibleInTree()) return false;
        ImpPlayer p = ImpPlayer.players[0];
        if (p.target_focus is C2_TextEdit { is_focused: true }) return false;
        for (ImpComp n = p.target_focus; n != null; n = n.parent)
            if (n == this) return true;
        return false;
    }

    void ApplyEngineContent()
    {
        bool attached = tree_dir_engine.parent == tab_dir;
        if (show_engine_content)
        {
            if (!attached) tab_dir.Child_Add(tree_dir_engine);
            return;
        }

        if (attached) tree_dir_engine.Detach();
        tab_dir.selected_tab = 0;
        if (IsUnder(_current_dir, tree_dir_engine.root_path))
            CurrentDir_Set(new TDirectory { path = tree_dir_game.root_path });
    }

    // -------------------------------------------------------------------
    // Search
    // -------------------------------------------------------------------

    /// <summary>
    /// Splits "type==Texture some name" into the type clause the old grid understood and the
    /// plain text left over, then hands both to the trees.
    /// </summary>
    void Query_Apply()
    {
        string q = _query?.Trim() ?? "";
        string type_filter = "";
        Match tm = Regex.Match(q, @"type\s*==?\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
        if (tm.Success)
        {
            type_filter = tm.Groups[1].Value;
            q = q.Remove(tm.Index, tm.Length).Trim();
        }
        tree_dir_game.Filter_Set(q, type_filter);
        tree_dir_engine.Filter_Set(q, type_filter);
    }

    // -------------------------------------------------------------------
    // Rows
    // -------------------------------------------------------------------

    void _Item_Select(TTreeItem item)
    {
        if (item.data is TDirectory dir)
        {
            _selected_path = dir.path;
            CurrentDir_Set(dir);
            return;
        }
        if (item.data is not TFile file || string.IsNullOrEmpty(file.path)) return;
        _selected_path = file.path;
        // The folder a new scene or asset would land in follows the selection, so "New Scene"
        // from a file row puts it next to that file.
        string parent = Path.GetDirectoryName(file.path);
        if (!string.IsNullOrEmpty(parent)) CurrentDir_Set(new TDirectory { path = parent });
    }

    void _Item_Open(TTreeItem item)
    {
        if (item.data is TDirectory dir)
        {
            EdFileTree tree = TreeForPath(dir.path);
            tree?.Tree_ExpandKey(dir.path, !tree.Tree_IsExpanded(dir.path));
            return;
        }
        if (item.data is TFile file) File_For(file.path)?.Editor_File_Open();
    }

    void _Item_RightClick(TTreeItem item)
    {
        Vector2 pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;

        if (item.data is TDirectory dir && !string.IsNullOrEmpty(dir.path))
        {
            _selected_path = dir.path;
            ImpPlayer.Popup_Run(this, new A_PopupConfig { options = FolderOptions(dir.path) }, null, pos);
            return;
        }
        if (item.data is not TFile file || string.IsNullOrEmpty(file.path)) return;

        _selected_path = file.path;
        ImpPlayer.Popup_Run(this, new A_PopupConfig { options = FileOptions(file.path) }, null, pos);
    }

    void _Item_DropExternal(object payload, TTreeItem item)
    {
        if (payload is not string src) return;
        if (item.data is TDirectory dir) Path_MoveInto(src, dir.path);
        //Dropping onto a file means "put it beside this", same as dropping onto its folder.
        else if (item.data is TFile file) Path_MoveInto(src, Path.GetDirectoryName(file.path));
    }

    object _Item_DragPayload(TTreeItem item)
    {
        string path = item.data switch
        {
            TDirectory d => d.path,
            TFile f => f.path,
            _ => null,
        };
        if (string.IsNullOrEmpty(path)) return null;
        // The two content roots stay put.
        if (PathsEqual(path, tree_dir_game.root_path) || PathsEqual(path, tree_dir_engine.root_path))
            return null;
        return path;
    }

    /// <summary>The asset or source file behind a path, or null when it is neither.</summary>
    internal static I_File File_For(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        string ext = Path.GetExtension(path);
        if (ext.StartsWith(".Imp", StringComparison.OrdinalIgnoreCase))
        {
            ImpAsset asset = ImpAsset.Load(path);
            if (asset != null) return asset;
        }
        return ImpFile.GetOrCreate(path);
    }

    // -------------------------------------------------------------------
    // Drag & drop
    // -------------------------------------------------------------------

    EdDragGhost _ghost;

    // Driven from OnUpdate rather than from a grab callback, so a drag between the two trees and a
    // drag out into a viewport both get the same ghost.
    void Drag_Sync()
    {
        if (ImpPlayer.players.Count == 0) { Ghost_Hide(); return; }
        ImpPlayer p = ImpPlayer.players[0];

        string path = p.grab_is_active ? p.target_grabbed?.CursorGrab_Payload() as string : null;
        if (string.IsNullOrEmpty(path) || !Drag_IsOurs(p.target_grabbed))
        {
            Ghost_Hide();
            return;
        }

        if (_ghost == null)
        {
            ImpComp host = C2_MenuBar.PopupHost();
            if (host == null) return;
            _ghost = new EdDragGhost();
            host.Child_Add(_ghost);
        }

        _ghost.label = Path.GetFileName(
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        _ghost.accent = Directory.Exists(path)
            ? new Color(120, 170, 220, 255)
            : File_For(path)?.Editor_GetThumbnail_Color() ?? new Color(120, 170, 220, 255);
        //Placed here rather than in the ghost's own update, so it never shows at the origin first.
        _ghost.transform.position = p.cursor.position + new Vector2(14, 12);
        _ghost.is_valid = Drop_TargetIsValid(p.target_cursor, path);
    }

    void Ghost_Hide()
    {
        _ghost?.Destroy();
        _ghost = null;
    }

    bool Drag_IsOurs(ImpComp c)
    {
        for (ImpComp n = c; n != null; n = n.parent)
            if (n == this) return true;
        return false;
    }

    bool Drop_TargetIsValid(ImpComp target, string path)
    {
        if (target == null) return false;
        if (target is C2_Picker picker) return picker.Drop_Accepts(path);
        if (target is PNL_SceneView)
        {
            return ImpAsset.Load(path) is ImpScene or A_Mesh;
        }
        if (target is C2_Viewport3D)
        {
            return ImpAsset.Load(path) is ImpScene or A_Mesh;
        }
        if (target is C2_Viewport2D)
        {
            return ImpAsset.Load(path) is ImpScene;
        }
        // Tree rows are internal to the engine, so identify them by the tree they sit in.
        for (ImpComp n = target; n != null; n = n.parent)
            if (n is EdFileTree) return true;
        return false;
    }

    /// <summary>Moves a file or folder into dest_dir, renaming on collision.</summary>
    internal void Path_MoveInto(string src, string dest_dir)
    {
        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dest_dir)) return;

        string from, to_dir;
        try
        {
            from = Path.GetFullPath(src);
            to_dir = Path.GetFullPath(dest_dir);
        }
        catch { return; }

        bool is_dir = Directory.Exists(from);
        if (!is_dir && !File.Exists(from)) return;
        if (!Directory.Exists(to_dir)) return;
        if (PathsEqual(Path.GetDirectoryName(from) ?? "", to_dir)) return;
        // A folder cannot be moved inside itself.
        if (is_dir && IsUnder(to_dir, from)) return;

        string name = Path.GetFileName(from.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string to = Path.Combine(to_dir, name);
        if (File.Exists(to) || Directory.Exists(to))
        {
            //A folder called "My.Stuff" keeps its whole name; only files split off an extension.
            string bare = is_dir ? name : Path.GetFileNameWithoutExtension(name);
            string ext = is_dir ? "" : Path.GetExtension(name);
            to = UniqueName(to_dir, bare, ext);
        }

        try
        {
            if (is_dir) Directory.Move(from, to);
            else File.Move(from, to);
        }
        catch { return; }

        ImpAsset.Cache_Rekey(from, to);
        ImpFile.Cache_Rekey(from, to);
        EdFolderColors.Path_Moved(from, to);
        Browsers_Notify(from, to);
    }

    // -------------------------------------------------------------------
    // Rename
    // -------------------------------------------------------------------

    /// <summary>Opens the rename field over the row, or at the cursor when the row is off screen.</summary>
    void Rename_Begin(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        bool is_dir = Directory.Exists(path);
        if (!is_dir && !File.Exists(path)) return;
        // The two content roots stay put, same as for a drag.
        if (PathsEqual(path, tree_dir_game.root_path) || PathsEqual(path, tree_dir_engine.root_path)) return;

        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string shown = is_dir || show_file_extensions ? name : Path.GetFileNameWithoutExtension(name);

        object data = is_dir ? new TDirectory { path = path } : new TFile { path = path };
        Vector2 pos, size;
        if (TreeForPath(path) is { } tree && tree.Row_Rect(data, out TDimensions2 dim))
        {
            pos = dim.position + new Vector2(2, 1);
            size = new Vector2(MathF.Max(dim.size.X - 4, 130), MathF.Max(18, dim.size.Y - 2));
        }
        else
        {
            pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
            size = new Vector2(180, 22);
        }

        string captured = path;
        EdRenameField.Open(pos, size, shown,
            text => Rename_IsValid(captured, text),
            text => Path_Rename(captured, text));
    }

    /// <summary>The name a rename would land on, or null when what was typed cannot be one.</summary>
    string Rename_Resolve(string path, string text)
    {
        string name = (text ?? "").Trim();
        if (string.IsNullOrEmpty(name) || name is "." or "..") return null;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        if (Directory.Exists(path)) return name;

        // The field only ever edits what the row shows, so a hidden extension is put back rather
        // than left to whatever the typed name happened to end in.
        string ext = Path.GetExtension(path);
        if (!show_file_extensions) return name + ext;
        return string.IsNullOrEmpty(Path.GetExtension(name)) ? name + ext : name;
    }

    bool Rename_IsValid(string path, string text)
    {
        string name = Rename_Resolve(path, text);
        if (name == null) return false;

        string from, dest;
        try
        {
            from = Path.GetFullPath(path);
            dest = Path.Combine(Path.GetDirectoryName(from) ?? "", name);
        }
        catch { return false; }

        //Landing back on itself - including a change of casing only - is not a collision.
        if (PathsEqual(from, dest)) return true;
        return !File.Exists(dest) && !Directory.Exists(dest);
    }

    /// <summary>Renames a file or folder in place and repoints every reference to it.</summary>
    internal void Path_Rename(string path, string text)
    {
        if (string.IsNullOrEmpty(path)) return;
        string from;
        try { from = Path.GetFullPath(path); }
        catch { return; }

        bool is_dir = Directory.Exists(from);
        if (!is_dir && !File.Exists(from)) return;
        if (PathsEqual(from, tree_dir_game.root_path) || PathsEqual(from, tree_dir_engine.root_path)) return;

        string name = Rename_Resolve(from, text);
        if (name == null) return;

        string dir = Path.GetDirectoryName(from) ?? "";
        string to = Path.Combine(dir, name);
        if (string.Equals(from, to, StringComparison.Ordinal)) return;
        bool case_only = PathsEqual(from, to);
        if (!case_only && (File.Exists(to) || Directory.Exists(to))) return;

        try
        {
            if (case_only)
            {
                // Only the casing changed, so the filesystem reads source and destination as the
                // same entry and refuses the move. It goes by way of a name nothing else holds.
                string temp = UniqueName(dir, name + "~rename", "");
                if (is_dir) { Directory.Move(from, temp); Directory.Move(temp, to); }
                else { File.Move(from, temp); File.Move(temp, to); }
            }
            else if (is_dir) Directory.Move(from, to);
            else File.Move(from, to);
        }
        catch { return; }

        ImpAsset.Cache_Rekey(from, to);
        ImpFile.Cache_Rekey(from, to);
        EdFolderColors.Path_Moved(from, to);
        ImpRefs.Repath(from, to);
        Browsers_Notify(from, to);
    }

    /// <summary>Where a path ends up once from has become to. Unchanged when it is not involved.</summary>
    static string Path_Moved(string path, string from, string to)
    {
        if (string.IsNullOrEmpty(path)) return path;
        string full;
        try { full = Path.GetFullPath(path); }
        catch { return path; }
        if (PathsEqual(full, from)) return to;
        string root = from.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return to + full[root.Length..];
        return path;
    }

    public void CurrentDir_Set(TDirectory dir)
    {
        string path = dir.path ?? "";
        if (string.IsNullOrWhiteSpace(path)) return;
        try { path = Path.GetFullPath(path); }
        catch { return; }
        _current_dir = path;

        // A selection only survives a move of the current dir when it is the folder itself or
        // something sitting directly in it - navigating anywhere else drops it.
        if (!PathsEqual(_selected_path, path)
            && !PathsEqual(Path.GetDirectoryName(_selected_path) ?? "", path))
        {
            _selected_path = "";
        }
        SyncTreeSelection();
    }

    public void RefreshAll()
    {
        tree_dir_game.Refresh();
        tree_dir_engine.Refresh();
    }

    public void State_Capture(EdStateBrowser data)
    {
        if (data == null)
        {
            return;
        }
        data.current_dir = EdState.Path_Store(_current_dir);
        data.dir_tab = tab_dir.selected_tab;
        data.search = _query ?? "";
        data.show_source_files = show_source_files;
        data.show_file_extensions = show_file_extensions;
        data.show_engine_content = show_engine_content;
        data.row_height = row_height;
        data.settings_open = _settings.is_expanded;
        data.expanded.Clear();
        AddExpanded(tree_dir_game, data.expanded);
        AddExpanded(tree_dir_engine, data.expanded);
    }

    public void State_Apply(EdStateBrowser data)
    {
        if (data == null)
        {
            return;
        }

        show_source_files = data.show_source_files;
        show_file_extensions = data.show_file_extensions;
        show_engine_content = data.show_engine_content;
        if (data.row_height >= 16)
        {
            row_height = data.row_height;
        }
        _last_src = show_source_files;
        _last_ext = show_file_extensions;
        _last_engine = show_engine_content;
        _last_row_height = row_height;
        Tree_Wire(tree_dir_game);
        Tree_Wire(tree_dir_engine);
        ApplyEngineContent();

        string game_root = ImpFile.ContentDir_Game();
        string engine_root = ImpFile.ContentDir_Engine();
        if (Directory.Exists(game_root) && !PathsEqual(game_root, engine_root) && !IsUnder(game_root, engine_root))
        {
            tree_dir_game.root_path = game_root;
        }
        tree_dir_engine.root_path = engine_root;
        tree_dir_game.RebuildFromDisk();
        tree_dir_engine.RebuildFromDisk();

        List<string> game_keys = new();
        List<string> engine_keys = new();
        for (int i = 0; i < data.expanded.Count; i++)
        {
            string p = EdState.Path_Load(data.expanded[i]);
            if (string.IsNullOrEmpty(p))
            {
                continue;
            }
            if (IsUnder(p, tree_dir_engine.root_path))
            {
                engine_keys.Add(p);
            }
            else
            {
                game_keys.Add(p);
            }
        }
        tree_dir_game.Tree_SetExpandedKeys(game_keys);
        tree_dir_engine.Tree_SetExpandedKeys(engine_keys);

        _settings.is_expanded = data.settings_open;

        string dir = EdState.Path_Load(data.current_dir);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            dir = tree_dir_game.root_path;
        }
        if (IsUnder(dir, tree_dir_engine.root_path) && !IsUnder(dir, tree_dir_game.root_path))
        {
            tab_dir.selected_tab = show_engine_content ? 1 : 0;
            if (!show_engine_content)
            {
                dir = tree_dir_game.root_path;
            }
        }
        else
        {
            tab_dir.selected_tab = 0;
        }
        CurrentDir_Set(new TDirectory { path = dir });
        ExpandAncestors(dir);
        if (!string.IsNullOrEmpty(data.search))
        {
            _search.Query_Set(data.search);
        }
    }

    void ExpandAncestors(string path)
    {
        EdFileTree tree = TreeForPath(path);
        if (tree == null || string.IsNullOrEmpty(path) || string.IsNullOrEmpty(tree.root_path))
        {
            return;
        }
        string full;
        string root;
        try
        {
            full = Path.GetFullPath(path);
            root = Path.GetFullPath(tree.root_path);
        }
        catch
        {
            return;
        }
        tree.Tree_ExpandKey(root, true);
        string rel;
        try
        {
            rel = Path.GetRelativePath(root, full);
        }
        catch
        {
            return;
        }
        if (rel == "." || rel.StartsWith(".."))
        {
            return;
        }
        string cur = root;
        string[] parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i]))
            {
                continue;
            }
            cur = Path.Combine(cur, parts[i]);
            tree.Tree_ExpandKey(cur, true);
        }
    }

    static void AddExpanded(EdFileTree tree, List<string> dest)
    {
        if (tree == null)
        {
            return;
        }
        List<string> keys = tree.Tree_ExpandedKeys();
        for (int i = 0; i < keys.Count; i++)
        {
            string stored = EdState.Path_Store(keys[i]);
            if (!string.IsNullOrEmpty(stored) && !dest.Contains(stored))
            {
                dest.Add(stored);
            }
        }
    }

    // The highlight follows what was actually clicked, which for a file row is the file - the
    // current dir has already moved to its folder by then and would drag the highlight with it.
    void SyncTreeSelection()
    {
        string path = string.IsNullOrEmpty(_selected_path) ? _current_dir : _selected_path;
        if (string.IsNullOrEmpty(path)) return;
        EdFileTree tree = TreeForPath(path);
        if (tree == null) return;
        tree.Tree_SelectData(Directory.Exists(path)
            ? new TDirectory { path = path }
            : new TFile { path = path });
    }

    EdFileTree TreeForPath(string path)
    {
        if (IsUnder(path, tree_dir_game.root_path)) return tree_dir_game;
        if (IsUnder(path, tree_dir_engine.root_path)) return tree_dir_engine;
        return tab_dir.selected_tab == 0 ? tree_dir_game : tree_dir_engine;
    }

    // -------------------------------------------------------------------
    // Menus
    // -------------------------------------------------------------------

    List<TPopupMenuOption> FolderOptions(string path)
    {
        return new List<TPopupMenuOption>
        {
            new() { text = "Open", on_press = () => CurrentDir_Set(new TDirectory { path = path }) },
            new() { text = "Folder Color", suboptions = ColorOptions(path) },
            new() { is_separator = true },
            new() { text = "New Folder", on_press = NewFolder },
            new() { text = "New Scene", on_press = NewScene },
            new() { text = "New Asset", on_press = NewAsset },
            new() { text = "Import Sources as Assets", on_press = ImportSources },
            new() { is_separator = true },
            new() { text = "Rename", on_press = () => Rename_Begin(path) },
            new() { text = "Duplicate", on_press = () => Path_Duplicate(path) },
            new() { text = "Show in Explorer", on_press = () => ShowInExplorer(path, false) },
            new() { text = "Delete", on_press = () => Path_DeleteAsk(path) },
        };
    }

    /// <summary>
    /// A fixed palette rather than a picker, matching Godot. Clearing a folder drops it back to
    /// whatever colour its nearest coloured ancestor is handing down.
    /// </summary>
    List<TPopupMenuOption> ColorOptions(string path)
    {
        List<TPopupMenuOption> opts = new();
        for (int i = 0; i < EdFolderColors.PALETTE.Length; i++)
        {
            (string label, Color color) = EdFolderColors.PALETTE[i];
            opts.Add(new TPopupMenuOption { text = label, on_press = () => Color_Set(path, color) });
        }
        opts.Add(new TPopupMenuOption { is_separator = true });
        opts.Add(new TPopupMenuOption
        {
            text = "Clear",
            is_disabled = EdFolderColors.Get(path).A == 0,
            on_press = () => Color_Set(path, null),
        });
        return opts;
    }

    void Color_Set(string path, Color? color)
    {
        EdFolderColors.Set(path, color);
        //Every browser shares the store, so they all owe a rebuild.
        Browsers_Notify();
    }

    List<TPopupMenuOption> FileOptions(string path)
    {
        I_File file = File_For(path);
        List<TPopupMenuOption> opts = file?.Editor_File_GetOptions() ?? new List<TPopupMenuOption>();
        if (opts.Count == 0)
        {
            opts.Add(new() { text = "Open", on_press = () => file?.Editor_File_Open() });
        }
        if (file is ImpFile src && src.default_asset_type != null)
            opts.Add(new() { text = "Create Asset", on_press = () => { src.Editor_CreateAsset(); Browsers_Notify(); } });
        opts.Add(new() { is_separator = true });
        opts.Add(new() { text = "Rename", on_press = () => Rename_Begin(path) });
        opts.Add(new() { text = "Duplicate", on_press = () => Path_Duplicate(path) });
        opts.Add(new() { text = "Show in Explorer", on_press = () => ShowInExplorer(path, true) });
        opts.Add(new() { text = "Delete", on_press = () => Path_DeleteAsk(path) });
        return opts;
    }

    List<TPopupMenuOption> EmptyFolderOptions()
    {
        return new List<TPopupMenuOption>
        {
            new() { text = "New Folder", on_press = NewFolder },
            new() { text = "New Scene", on_press = NewScene },
            new() { text = "New Asset", on_press = NewAsset },
            new() { text = "Import Sources as Assets", on_press = ImportSources },
            new() { is_separator = true },
            new() { text = "Refresh", on_press = RefreshAll },
        };
    }

    void NewFolder()
    {
        if (string.IsNullOrEmpty(_current_dir) || !Directory.Exists(_current_dir)) return;
        string dest = UniqueName(_current_dir, "NewFolder", "");
        Directory.CreateDirectory(dest);
        Browsers_Notify();
    }

    void NewScene()
    {
        DLG_NewScene.Run(_current_dir);
    }

    void NewAsset()
    {
        DLG_NewAsset.Run(_current_dir);
    }

    void ImportSources()
    {
        if (File_For(_selected_path) is ImpFile selected_src)
        {
            selected_src.Editor_CreateAsset();
            Browsers_Notify();
            return;
        }
        if (string.IsNullOrEmpty(_current_dir) || !Directory.Exists(_current_dir)) return;
        foreach (string file in Directory.GetFiles(_current_dir))
        {
            string ext = Path.GetExtension(file);
            if (ext.StartsWith(".Imp", StringComparison.OrdinalIgnoreCase)) continue;
            ImpFile src = ImpFile.Create_FromExtension(file);
            if (src == null || src.default_asset_type == null) continue;
            src = ImpFile.GetOrCreate(file);
            string asset_ext = ((ImpAsset)Activator.CreateInstance(src.default_asset_type)!).File_GetExtension();
            string sibling = Path.Combine(_current_dir, Path.GetFileNameWithoutExtension(file) + "." + asset_ext);
            if (File.Exists(sibling)) continue;
            src.Editor_CreateAsset();
        }
        Browsers_Notify();
    }

    /// <summary>Asks before deleting, except empty folders which go straight through.</summary>
    internal void Path_DeleteAsk(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (PathsEqual(path, tree_dir_game.root_path) || PathsEqual(path, tree_dir_engine.root_path)) return;

        bool is_dir = Directory.Exists(path);
        if (!is_dir && !File.Exists(path)) return;

        if (is_dir)
        {
            bool empty = false;
            try { empty = Directory.GetFileSystemEntries(path).Length == 0; }
            catch { }
            if (empty)
            {
                Path_Delete(path);
                return;
            }
            string captured = path;
            DLG_ConfirmDelete.Run("Delete Folder and its contents? Cannot be undone.", () => Path_Delete(captured));
            return;
        }

        string file_path = path;
        DLG_ConfirmDelete.Run("Delete File? Cannot be undone.", () => Path_Delete(file_path));
    }

    /// <summary>Deletes a file or folder on disk and drops every cache entry that lived there.</summary>
    internal void Path_Delete(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        string from;
        try { from = Path.GetFullPath(path); }
        catch { return; }

        if (PathsEqual(from, tree_dir_game.root_path) || PathsEqual(from, tree_dir_engine.root_path)) return;

        bool is_dir = Directory.Exists(from);
        if (!is_dir && !File.Exists(from)) return;

        try
        {
            if (is_dir) Directory.Delete(from, true);
            else File.Delete(from);
        }
        catch { return; }

        ImpAsset.Cache_Drop(from);
        ImpFile.Cache_Drop(from);
        EdFolderColors.Path_Dropped(from);
        Browsers_Notify(from);
    }

    /// <summary>Copies a file or folder next to the original under a unique name.</summary>
    internal void Path_Duplicate(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        string from;
        try { from = Path.GetFullPath(path); }
        catch { return; }

        bool is_dir = Directory.Exists(from);
        if (!is_dir && !File.Exists(from)) return;
        if (PathsEqual(from, tree_dir_game.root_path) || PathsEqual(from, tree_dir_engine.root_path)) return;

        string dir = Path.GetDirectoryName(from) ?? "";
        string name = Path.GetFileName(from.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string bare = is_dir ? name : Path.GetFileNameWithoutExtension(name);
        string ext = is_dir ? "" : Path.GetExtension(name);
        string to = UniqueName(dir, bare, ext);

        try
        {
            if (is_dir)
            {
                void CopyDir(string src, string dest)
                {
                    Directory.CreateDirectory(dest);
                    string[] files = Directory.GetFiles(src);
                    for (int i = 0; i < files.Length; i++)
                    {
                        File.Copy(files[i], Path.Combine(dest, Path.GetFileName(files[i])));
                    }
                    string[] dirs = Directory.GetDirectories(src);
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        CopyDir(dirs[i], Path.Combine(dest, Path.GetFileName(dirs[i])));
                    }
                }
                CopyDir(from, to);
            }
            else
            {
                File.Copy(from, to);
            }
        }
        catch { return; }

        Browsers_Notify();
    }

    static void ShowInExplorer(string path, bool select)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = select ? "/select,\"" + path + "\"" : "\"" + path + "\"",
                UseShellExecute = true,
            });
        }
        catch { }
    }

    static string UniqueName(string dir, string base_name, string ext)
    {
        string dest = Path.Combine(dir, base_name + ext);
        int i = 1;
        while (File.Exists(dest) || Directory.Exists(dest))
        {
            dest = Path.Combine(dir, base_name + "_" + i + ext);
            i++;
        }
        return dest;
    }

    internal static string SourceTypeLabel(string ext, ImpFile file)
    {
        ext = (ext ?? "").TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" or "jpg" or "jpeg" or "tga" or "bmp" => "Texture Source",
            "hdr" or "exr" => "HDR Source",
            "ttf" or "otf" => "Font Source",
            "glb" or "gltf" or "fbx" or "obj" => "Model Source",
            "wav" or "ogg" or "mp3" => "Sound Source",
            _ => file.file_type != 0 ? file.file_type + " Source" : (string.IsNullOrEmpty(ext) ? "File" : ext.ToUpperInvariant()),
        };
    }

    public static bool ShouldSkipName(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        if (name[0] == '.') return true;
        if (name is "bin" or "obj" or "node_modules" or "Thumbs.db" or "desktop.ini") return true;
        return false;
    }

    public static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    // Game Content folder for new files. Engine content is read-from, not written-to.
    public static string Folder_ForCreate(string suggested)
    {
        string game = ImpFile.ContentDir_Game();
        string folder = suggested;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            folder = game;
        }

        string engine = ImpFile.ContentDir_Engine();
        if (!string.IsNullOrEmpty(engine)
            && !string.IsNullOrEmpty(game)
            && IsUnder(folder, engine)
            && !IsUnder(folder, game)
            && !PathsEqual(folder, game))
        {
            folder = game;
        }

        if (string.IsNullOrEmpty(folder))
        {
            return "";
        }
        if (!Directory.Exists(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch
            {
                return "";
            }
        }
        return folder;
    }

    public static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;
        try
        {
            string p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return p.Equals(r, StringComparison.OrdinalIgnoreCase)
                   || p.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}

// ====================================================================================================================
// File Tree
// ====================================================================================================================

/// <summary>
/// A content root as one tree of folders and files. Folder colours cascade: a folder with no colour
/// of its own draws in whatever its nearest coloured ancestor is handing down, which is resolved
/// here as the tree is built rather than stored per folder.
/// </summary>
public class EdFileTree : C2_Tree
{
    public string root_path;
    public Action on_rebuilt;
    public bool show_source_files = true;
    public bool show_file_extensions;

    static readonly Color TypeLabelColor = new(130, 130, 130, 255);

    string _built_path;
    int _built_colors = -1;
    bool _dirty = true;
    string _filter = "";
    string _type_filter = "";
    //Expansion as it stood before a search took over, put back when the search box is cleared.
    List<string> _pre_filter_expanded;

    bool IsFiltering => _filter.Length > 0 || _type_filter.Length > 0;

    public void Refresh()
    {
        _dirty = true;
    }

    /// <summary>Narrows the tree to matching files, keeping the folders needed to reach them.</summary>
    public void Filter_Set(string filter, string type_filter)
    {
        filter = filter?.Trim() ?? "";
        type_filter = type_filter?.Trim() ?? "";
        if (filter == _filter && type_filter == _type_filter) return;

        bool was = IsFiltering;
        _filter = filter;
        _type_filter = type_filter;
        if (IsFiltering && !was) _pre_filter_expanded = Tree_ExpandedKeys();
        _dirty = true;
    }

    public bool Tree_IsExpanded(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        List<string> keys = Tree_ExpandedKeys();
        for (int i = 0; i < keys.Count; i++)
            if (string.Equals(keys[i], key, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public override void OnUpdate(double dt)
    {
        if (_dirty
            || !string.Equals(_built_path, root_path, StringComparison.OrdinalIgnoreCase)
            || _built_colors != EdFolderColors.version)
        {
            RebuildFromDisk();
        }
        base.OnUpdate(dt);
    }

    public void RebuildFromDisk()
    {
        _built_path = root_path;
        _built_colors = EdFolderColors.version;
        _dirty = false;

        object keep = selected_data;
        Tree_Clear();
        if (string.IsNullOrEmpty(root_path) || !Directory.Exists(root_path)) return;

        TTreeItem root = BuildDir(root_path, true, default);
        if (IsFiltering) root = Item_Prune(root, false);
        Tree_Add(root);

        if (IsFiltering)
        {
            Tree_ExpandAll(true);
        }
        else if (_pre_filter_expanded != null)
        {
            Tree_SetExpandedKeys(_pre_filter_expanded);
            _pre_filter_expanded = null;
        }
        Tree_ExpandKey(KeyOf(root.data, root), true);

        if (keep != null) Tree_SelectData(keep);
        on_rebuilt?.Invoke();
    }

    TTreeItem BuildDir(string path, bool is_root, Color inherited)
    {
        string label = is_root
            ? (string.IsNullOrEmpty(name) ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) : name)
            : Path.GetFileName(path);
        if (string.IsNullOrEmpty(label)) label = path;

        Color own = EdFolderColors.Get(path);
        Color tint = own.A > 0 ? own : inherited;

        List<TTreeItem> kids = new();
        try
        {
            string[] dirs = Directory.GetDirectories(path);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirs.Length; i++)
            {
                string n = Path.GetFileName(dirs[i]);
                if (PNL_FileBrowser.ShouldSkipName(n)) continue;
                kids.Add(BuildDir(dirs[i], false, tint));
            }

            //Files after folders, the order the grid used to sort them into.
            string[] files = Directory.GetFiles(path);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                string n = Path.GetFileName(files[i]);
                if (PNL_FileBrowser.ShouldSkipName(n)) continue;
                if (BuildFile(files[i], tint) is { } item) kids.Add(item);
            }
        }
        catch { }

        return new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection
                {
                    text = label,
                    icon = A_Texture.THUMB_FOLDER,
                    icon_expanded = A_Texture.THUMB_FOLDER_OPEN,
                    icon_tint = tint,
                }
            },
            children = kids.ToArray(),
            data = new TDirectory { path = path },
            row_tint = tint,
        };
    }

    // Files keep their own type colour on the icon and only take the folder colour as the row
    // wash, so a coloured folder never hides what kind of asset something is.
    TTreeItem? BuildFile(string path, Color inherited)
    {
        string name = Path.GetFileName(path);
        string ext = Path.GetExtension(path);
        string label;
        string type;
        Color tint;
        Texture2D? thumb = null;

        if (ext.Equals(".ImpAsset", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ImpScene", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ImpGame", StringComparison.OrdinalIgnoreCase))
        {
            ImpAsset asset = ImpAsset.Load(path);
            if (asset == null)
            {
                if (ext.Equals(".ImpScene", StringComparison.OrdinalIgnoreCase))
                {
                    asset = new ImpScene { filepath = path };
                }
                else
                {
                    asset = new ImpAsset(path);
                }
            }
            type = asset.Editor_GetTypeLabel();
            label = show_file_extensions ? name : asset.GetName();
            tint = asset.Editor_GetThumbnail_Color();
            thumb = asset.Editor_GetThumbnail_Texture();
        }
        else
        {
            if (!show_source_files) return null;
            ImpFile src = ImpFile.GetOrCreate(path);
            type = PNL_FileBrowser.SourceTypeLabel(ext, src);
            label = show_file_extensions ? name : Path.GetFileNameWithoutExtension(name);
            tint = src.Editor_GetThumbnail_Color();
            thumb = src.Editor_GetThumbnail_Texture();
        }

        bool has_thumb = thumb.HasValue && thumb.Value.Id != 0;
        Color icon_tint = tint;
        if (has_thumb)
        {
            icon_tint = default;
        }

        return new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection
                {
                    text = label,
                    icon = A_Texture.THUMB_FILE,
                    icon_texture = thumb,
                    icon_tint = icon_tint,
                },
                new TTreeItemSection
                {
                    text = type,
                    color = TypeLabelColor,
                },
            },
            children = Array.Empty<TTreeItem>(),
            data = new TFile { path = path },
            row_tint = inherited,
        };
    }

    bool Item_Matches(TTreeItem item)
    {
        TTreeItemSection[] s = item.sections;
        string label = s is { Length: > 0 } ? s[0].text ?? "" : "";
        string type = s is { Length: > 1 } ? s[1].text ?? "" : "";

        if (_type_filter.Length > 0
            && type.IndexOf(_type_filter, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }
        if (_filter.Length == 0) return true;
        return label.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0
               || type.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool Item_Visible(TTreeItem item)
    {
        if (Item_Matches(item)) return true;
        TTreeItem[] kids = item.children;
        for (int i = 0; kids != null && i < kids.Length; i++)
            if (Item_Visible(kids[i])) return true;
        return false;
    }

    // A folder that matches by name brings its whole contents along; otherwise only the branches
    // holding a match survive.
    TTreeItem Item_Prune(TTreeItem item, bool take_all)
    {
        bool self = take_all || Item_Matches(item);
        List<TTreeItem> kids = new();
        TTreeItem[] src = item.children;
        for (int i = 0; src != null && i < src.Length; i++)
        {
            if (self || Item_Visible(src[i])) kids.Add(Item_Prune(src[i], self));
        }
        item.children = kids.ToArray();
        return item;
    }
}

// Inline rename box. Like the drag ghost it lives on the popup host, so it draws over the tree,
// is not clipped by the panel, and outlives the refresh its own commit sets off.
public class EdRenameField : C2_Box
{
    public Func<string, bool> is_valid;
    public Action<string> on_commit;

    static EdRenameField _open;

    public static bool IsOpen => _open != null;

    readonly C2_TextEdit _edit = new()
    {
        layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Fill,
        },
        style = new UI_TextEdit
        {
            background = new UI_Box { tint = new Color(18, 18, 18, 255) },
            text_style = UI_Text.LIGHT,
            placeholder_style = UI_Text.MUTED,
        },
    };

    public static void Open(Vector2 position, Vector2 size, string text,
        Func<string, bool> is_valid, Action<string> on_commit)
    {
        Close();
        ImpComp host = C2_MenuBar.PopupHost();
        if (host == null) return;

        EdRenameField field = new()
        {
            is_valid = is_valid,
            on_commit = on_commit,
            style = new UI_Box { tint = new Color(18, 18, 18, 255) },
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Start,
                orient_V = EUIViewportAlignment.Start,
                size = size,
                size_min = size,
            },
        };
        field.transform.position = position;
        field._edit.text = text ?? "";
        field._edit.cursor = field._edit.text.Length;
        field._edit.is_focused = true;
        field._edit.on_submit = _ => field.Commit();
        field.Child_Add(field._edit);
        host.Child_Add(field);

        //Hogging the input keeps the click that opened the menu from moving the focus off the field.
        if (ImpPlayer.players.Count > 0) ImpPlayer.players[0].input_hog = field._edit;
        _open = field;
    }

    public static void Close()
    {
        if (_open == null) return;
        EdRenameField field = _open;
        _open = null;
        if (ImpPlayer.players.Count > 0 && ImpPlayer.players[0].input_hog == field._edit)
            ImpPlayer.players[0].input_hog = null;
        field.Destroy();
    }

    void Commit()
    {
        string text = _edit.text ?? "";
        if (is_valid != null && !is_valid(text)) return;
        Action<string> commit = on_commit;
        Close();
        commit?.Invoke(text);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (_open != this) return;
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Escape)) { Close(); return; }

        // The text field drops the caret itself once a click lands elsewhere. Take that as a
        // commit the way a file explorer does - unless the name could not be used, in which
        // case there is nothing to commit and the edit is abandoned.
        if (_edit.is_focused) return;
        if (is_valid != null && !is_valid(_edit.text ?? "")) Close();
        else Commit();
    }

    public override void OnDraw2DForeground(double dt, EDrawFlags flags)
    {
        base.OnDraw2DForeground(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        bool ok = is_valid == null || is_valid(_edit.text ?? "");
        Raylib.DrawRectangleLinesEx(
            new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 1.5f,
            ok ? new Color(0, 156, 227, 255) : new Color(210, 90, 90, 255));
    }
}

// Follows the cursor during a file browser drag. Lives on the popup host so it draws over
// everything and is never clipped by the browser panel.
public class EdDragGhost : Imp2D
{
    public string label = "";
    public Color accent = new(120, 170, 220, 255);
    public bool is_valid;

    const float GhostW = 190f;
    const float GhostH = 24f;

    public EdDragGhost()
    {
        cursor_filter = ECursorFilter.Ignore;
        layout.orient_H = EUIViewportAlignment.Start;
        layout.orient_V = EUIViewportAlignment.Start;
        layout.size = new Vector2(GhostW, GhostH);
        layout.size_min = layout.size;
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        Raylib.DrawRectangleV(dim.position, dim.size, new Color(28, 28, 30, 235));
        Raylib.DrawRectangleLinesEx(
            new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 1.5f,
            is_valid ? new Color(90, 220, 140, 255) : new Color(90, 90, 96, 255));
        Raylib.DrawRectangleV(dim.position + new Vector2(2, 2), new Vector2(3, dim.size.Y - 4), accent);

        UI_Text.LIGHT.Draw(label ?? "", dim.position + new Vector2(10, 0),
            new Vector2(dim.size.X - 28, dim.size.Y), 12, ETextWrap.None,
            EUIPositionAlignment.Center, EUIPositionAlignment.Start);

        float cx = dim.position.X + dim.size.X - 11;
        float cy = dim.position.Y + dim.size.Y * 0.5f;
        if (is_valid)
        {
            Raylib.DrawLineEx(new Vector2(cx - 4, cy), new Vector2(cx - 1, cy + 3), 2f, new Color(90, 220, 140, 255));
            Raylib.DrawLineEx(new Vector2(cx - 1, cy + 3), new Vector2(cx + 4, cy - 3), 2f, new Color(90, 220, 140, 255));
        }
        else
        {
            Color no = new(210, 90, 90, 255);
            Raylib.DrawLineEx(new Vector2(cx - 3, cy - 3), new Vector2(cx + 3, cy + 3), 2f, no);
            Raylib.DrawLineEx(new Vector2(cx - 3, cy + 3), new Vector2(cx + 3, cy - 3), 2f, no);
        }
    }
}
