using System.Numerics;
using System.Text.RegularExpressions;
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
    [ImpVar(Min = 10, Max = 160)] public float thumbnail_size = 80;

    C2_List _root = new()
    {
        alignment = EUIAlignment.Vertical,
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        spacing = 0,
    };

    C2_List _toolbar = new()
    {
        alignment = EUIAlignment.Horizontal,
        view_alighnment_H = EUIViewportAlignment.Fill,
        spacing = 4,
        size = new Vector2(0, 30),
        size_min = new Vector2(0, 30),
    };

    C2_List _crumbs = new()
    {
        alignment = EUIAlignment.Horizontal,
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        spacing = 0,
    };

    C2_List _body = new()
    {
        alignment = EUIAlignment.Horizontal,
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        spacing = 0,
    };

    C2_List _sidebar = new()
    {
        alignment = EUIAlignment.Vertical,
        view_alighnment_V = EUIViewportAlignment.Fill,
        size = new Vector2(220, 0),
        size_min = new Vector2(160, 0),
        spacing = 0,
    };

    C2_Expandable _favorites = new()
    {
        name = "Favorites",
        view_alighnment_H = EUIViewportAlignment.Fill,
        bar_height = 22,
        is_expanded = false,
    };

    C2_List _fav_list = new()
    {
        alignment = EUIAlignment.Vertical,
        view_alighnment_H = EUIViewportAlignment.Fill,
        spacing = 0,
    };

    C2_TabBox tab_dir = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
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

    C2_List _asset_col = new()
    {
        alignment = EUIAlignment.Vertical,
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        spacing = 2,
    };

    C2_SearchBar _search = new()
    {
        placeholder = "Search Assets",
        view_alighnment_H = EUIViewportAlignment.Fill,
        size = new Vector2(0, 26),
        size_min = new Vector2(0, 26),
    };

    C2_List file_list = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        alignment = EUIAlignment.Horizontal,
        is_scrollable = true,
        auto_scale_section_count = true,
        spacing = 8,
    };

    C2_Text _status = new()
    {
        text = "0 items",
        style = UiStyle_Text.MUTED,
        font_size_override = 11,
        text_alignment_h = EUIPositionAlignment.Start,
        wrap = ETextWrap.None,
        view_alighnment_H = EUIViewportAlignment.Fill,
        size = new Vector2(0, 20),
        size_min = new Vector2(0, 20),
    };

    C2_Inspector settings_inspector = new()
    {
        name = "Settings",
        view_alighnment_H = EUIViewportAlignment.Start,
        view_alighnment_V = EUIViewportAlignment.Fill,
        size = new Vector2(260, 0),
        size_min = new Vector2(200, 0),
        is_visible = false,
        declared_only = true,
        use_categories = false,
        show_header = false,
    };

    C2_Seperator _settings_split = new()
    {
        alignment = EUIAlignment.Horizontal,
        is_visible = false,
    };

    string _current_dir = "";
    string _query = "";
    bool _settings_open;
    EdFileThumbnail _selected;
    readonly List<string> _favorite_paths = new();

    bool _last_src = true;
    bool _last_ext;
    bool _last_engine = true;
    float _last_thumb = 80;

    static readonly UiStyle_Button ToolStyle = new()
    {
        style_unhovered = new UiStyle_Box { tint = new Color(48, 48, 48, 255) },
        style_hovered = new UiStyle_Box { tint = new Color(0, 120, 215, 255) },
        style_pressed = new UiStyle_Box { tint = new Color(0, 84, 153, 255) },
    };

    public PNL_FileBrowser()
    {
        name = "Content Browser";
        view_alighnment_H = EUIViewportAlignment.Fill;
        view_alighnment_V = EUIViewportAlignment.Fill;
        clip_children = true;
        style = new UiStyle_Box { tint = new Color(36, 36, 36, 255) };

        Child_Add(_root);
        _root.Child_Add(_toolbar);
        _root.Child_Add(_body);

        C2_Button add = ToolBtn("+ Add", 72);
        add.on_click = () =>
        {
            Vector2 p = add.Dimensions_Get().position;
            p.Y += add.Dimensions_Get().size.Y;
            C1_PopupMenu.Open(p, new List<TPopupMenuOption>
            {
                new() { text = "New Folder", on_press = NewFolder },
                new() { text = "New Scene", on_press = NewScene },
                new() { is_separator = true },
                new() { text = "Import Sources as Assets", on_press = ImportSources },
            });
        };
        _toolbar.Child_Add(add);
        _toolbar.Child_Add(ToolBtn("Import", 70, ImportSources));
        _toolbar.Child_Add(ToolBtn("Refresh", 72, RefreshAll));
        _toolbar.Child_Add(_crumbs);

        C2_Button settings_btn = ToolBtn("Settings", 80);
        settings_btn.on_click = () =>
        {
            _settings_open = !_settings_open;
            settings_inspector.is_visible = _settings_open;
            _settings_split.is_visible = _settings_open;
        };
        _toolbar.Child_Add(settings_btn);

        _favorites.Child_Add(_fav_list);
        RebuildFavorites();

        tab_dir.Child_Add(tree_dir_game);
        if (show_engine_content) tab_dir.Child_Add(tree_dir_engine);
        tree_dir_game.on_item_click = _DirSelect;
        tree_dir_engine.on_item_click = _DirSelect;
        tree_dir_game.on_item_right_click = _DirRightClick;
        tree_dir_engine.on_item_right_click = _DirRightClick;
        tree_dir_game.on_item_drop_external = _DirDropExternal;
        tree_dir_engine.on_item_drop_external = _DirDropExternal;
        tree_dir_game.item_drag_payload = _DirDragPayload;
        tree_dir_engine.item_drag_payload = _DirDragPayload;
        tree_dir_game.on_rebuilt = SyncTreeSelection;
        tree_dir_engine.on_rebuilt = SyncTreeSelection;
        tab_dir.on_tab_change = i =>
        {
            EdFileTree tree = i == 0 ? tree_dir_game : tree_dir_engine;
            string path = tree.selected_data is TDirectory d && !string.IsNullOrEmpty(d.path)
                ? d.path
                : tree.root_path;
            CurrentDir_Set(new TDirectory { path = path });
        };

        _sidebar.Child_Add(_favorites);
        _sidebar.Child_Add(tab_dir);

        C2_Seperator split = new()
        {
            alignment = EUIAlignment.Horizontal,
        };

        _search.on_search = q =>
        {
            _query = q ?? "";
            Grid_Refresh();
        };

        C2_Box grid_host = new()
        {
            style = new UiStyle_Box { tint = new Color(24, 24, 24, 255) },
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
            clip_children = true,
        };
        grid_host.Child_Add(file_list);

        _asset_col.Child_Add(_search);
        _asset_col.Child_Add(grid_host);
        _asset_col.Child_Add(_status);

        _body.Child_Add(_sidebar);
        _body.Child_Add(split);
        _body.Child_Add(_asset_col);
        _body.Child_Add(_settings_split);
        _body.Child_Add(settings_inspector);

        settings_inspector.Object_Add(this, true);

        tree_dir_game.root_path = ImpFile.ContentDir_Game();
        tree_dir_engine.root_path = ImpFile.ContentDir_Engine();
        string start = tree_dir_game.root_path;
        if (!Directory.Exists(start)) start = tree_dir_engine.root_path;
        CurrentDir_Set(new TDirectory { path = start });
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        Drag_Sync();

        string game_root = ImpFile.ContentDir_Game();
        string engine_root = ImpFile.ContentDir_Engine();
        if (!string.Equals(tree_dir_game.root_path, game_root, StringComparison.OrdinalIgnoreCase))
            tree_dir_game.root_path = game_root;
        if (!string.Equals(tree_dir_engine.root_path, engine_root, StringComparison.OrdinalIgnoreCase))
            tree_dir_engine.root_path = engine_root;

        if (_last_engine != show_engine_content)
        {
            _last_engine = show_engine_content;
            ApplyEngineContent();
        }

        if (_last_src != show_source_files || _last_ext != show_file_extensions
            || MathF.Abs(_last_thumb - thumbnail_size) > 0.25f)
        {
            _last_src = show_source_files;
            _last_ext = show_file_extensions;
            _last_thumb = thumbnail_size;
            Grid_Refresh();
        }

        if (ImpPlayer.Key_IsPressed(EInputKey.Mouse_Right) && ImpPlayer.players.Count > 0)
        {
            ImpPlayer p = ImpPlayer.players[0];
            if (p.Cursor_IsInDimensions(file_list.Dimensions_Get())
                && p.cursor_target is not EdFileThumbnail)
            {
                C1_PopupMenu.Open(p.cursor.position, EmptyFolderOptions());
            }
        }

        if (!EdRenameField.IsOpen && Focus_IsOurs() && ImpPlayer.Key_IsPressed(EInputKey.Key_F2))
            Rename_Selected();
    }

    // "The browser has the keyboard": the last thing clicked was inside this panel, and it was not
    // a text field that wants the keys for itself.
    bool Focus_IsOurs()
    {
        if (ImpPlayer.players.Count == 0 || !IsVisibleInTree()) return false;
        ImpPlayer p = ImpPlayer.players[0];
        if (p.ui_focus is C2_TextEdit { is_focused: true }) return false;
        for (ImpComp n = p.ui_focus; n != null; n = n.parent)
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

    C2_Button ToolBtn(string text, float width, Action on_click = null)
    {
        return new C2_Button
        {
            text = text,
            text_style = UiStyle_Text.LIGHT,
            override_font_size = 12,
            content_pad = 6,
            size = new Vector2(width, 26),
            size_min = new Vector2(width, 26),
            view_alighnment_V = EUIViewportAlignment.Center,
            style = ToolStyle,
            on_click = on_click,
        };
    }

    void _DirSelect(TTreeItem item)
    {
        if (item.data is TDirectory dir) CurrentDir_Set(dir);
    }

    void _DirRightClick(TTreeItem item)
    {
        if (item.data is not TDirectory dir || string.IsNullOrEmpty(dir.path)) return;
        Vector2 pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
        C1_PopupMenu.Open(pos, FolderOptions(dir.path));
    }

    void _DirDropExternal(object payload, TTreeItem item)
    {
        if (payload is not string src || item.data is not TDirectory dir) return;
        Path_MoveInto(src, dir.path);
    }

    // -------------------------------------------------------------------
    // Drag & drop
    // -------------------------------------------------------------------

    EdDragGhost _ghost;

    // Driven from OnUpdate rather than from a grab callback, so a drag out of the grid and a
    // drag out of the folder tree both get the same ghost.
    void Drag_Sync()
    {
        if (ImpPlayer.players.Count == 0) { Ghost_Hide(); return; }
        ImpPlayer p = ImpPlayer.players[0];

        string path = p.grab_is_active ? p.grabbed_target?.CursorGrab_Payload() as string : null;
        if (string.IsNullOrEmpty(path) || !Drag_IsOurs(p.grabbed_target))
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

        EdFileThumbnail thumb = p.grabbed_target as EdFileThumbnail;
        _ghost.label = thumb?.display_name ?? Path.GetFileName(
            path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        _ghost.accent = thumb?.type_color ?? new Color(120, 170, 220, 255);
        //Placed here rather than in the ghost's own update, so it never shows at the origin first.
        _ghost.transform.position = p.cursor.position + new Vector2(14, 12);
        _ghost.is_valid = Drop_TargetIsValid(p.cursor_target, path);
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
        if (target is EdFileThumbnail t) return t.is_folder && !PathsEqual(t.path, path);
        if (target is C2_Picker picker) return picker.Drop_Accepts(path);
        if (target is C2_SceneView) return ImpAsset.Load(path) is ImpScene or A_Mesh;
        // Tree rows are internal to the engine, so identify them by the tree they sit in.
        for (ImpComp n = target; n != null; n = n.parent)
            if (n is EdFileTree) return true;
        return false;
    }

    object _DirDragPayload(TTreeItem item)
    {
        if (item.data is not TDirectory d || string.IsNullOrEmpty(d.path)) return null;
        // The two content roots stay put.
        if (PathsEqual(d.path, tree_dir_game.root_path) || PathsEqual(d.path, tree_dir_engine.root_path))
            return null;
        return d.path;
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
        RefreshAll();
    }

    // -------------------------------------------------------------------
    // Rename
    // -------------------------------------------------------------------

    void Rename_Selected()
    {
        if (_selected == null) return;
        Rename_Begin(_selected.path, _selected);
    }

    /// <summary>Opens the rename field over a grid card, or at the cursor when there is no card.</summary>
    void Rename_Begin(string path, EdFileThumbnail thumb)
    {
        if (string.IsNullOrEmpty(path)) return;
        bool is_dir = Directory.Exists(path);
        if (!is_dir && !File.Exists(path)) return;
        // The two content roots stay put, same as for a drag.
        if (PathsEqual(path, tree_dir_game.root_path) || PathsEqual(path, tree_dir_engine.root_path)) return;

        string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string shown = is_dir || show_file_extensions ? name : Path.GetFileNameWithoutExtension(name);

        Vector2 pos, size;
        if (thumb != null)
        {
            //Sits over the card's name strip, laid out the same way EdFileThumbnail draws it.
            TDimensions2 dim = thumb.Dimensions_Get();
            float preview = MathF.Max(24, dim.size.Y - 40);
            float y = dim.position.Y + 4 + preview + 2;
            pos = new Vector2(dim.position.X + 2, y);
            size = new Vector2(MathF.Max(dim.size.X - 4, 130),
                MathF.Max(18, dim.position.Y + dim.size.Y - y - 2));
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

        // The field only ever edits what the grid shows, so a hidden extension is put back rather
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
        ImpRefs.Repath(from, to);

        for (int i = 0; i < _favorite_paths.Count; i++)
            _favorite_paths[i] = Path_Moved(_favorite_paths[i], from, to);
        RebuildFavorites();

        //Standing inside a folder that just got renamed means following it rather than going blank.
        string moved_dir = Path_Moved(_current_dir, from, to);
        if (!string.Equals(moved_dir, _current_dir, StringComparison.Ordinal))
            CurrentDir_Set(new TDirectory { path = moved_dir });
        RefreshAll();
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
        _selected = null;
        RebuildCrumbs();
        Grid_Refresh();
        SyncTreeSelection();
    }

    public void RefreshAll()
    {
        tree_dir_game.Refresh();
        tree_dir_engine.Refresh();
        Grid_Refresh();
    }

    void SyncTreeSelection()
    {
        EdFileTree tree = TreeForPath(_current_dir);
        tree?.Tree_SelectData(new TDirectory { path = _current_dir });
    }

    EdFileTree TreeForPath(string path)
    {
        if (IsUnder(path, tree_dir_game.root_path)) return tree_dir_game;
        if (IsUnder(path, tree_dir_engine.root_path)) return tree_dir_engine;
        return tab_dir.selected_tab == 0 ? tree_dir_game : tree_dir_engine;
    }

    void RebuildCrumbs()
    {
        _crumbs.Child_RemoveAll();
        EdFileTree tree = TreeForPath(_current_dir);
        string root = tree?.root_path ?? _current_dir;
        string root_name = tree?.name ?? "Content";

        void AddCrumb(string label, string path, bool last)
        {
            C2_Button btn = new()
            {
                text = last ? label : label + "  >",
                text_style = last ? UiStyle_Text.LIGHT : UiStyle_Text.MUTED,
                override_font_size = 12,
                content_pad = 4,
                size = new Vector2(MathF.Max(28, label.Length * 8 + (last ? 12 : 22)), 24),
                size_min = new Vector2(24, 24),
                view_alighnment_V = EUIViewportAlignment.Center,
                style = new UiStyle_Button
                {
                    style_unhovered = new UiStyle_Box { tint = new Color(36, 36, 36, 0) },
                    style_hovered = new UiStyle_Box { tint = new Color(0, 96, 166, 180) },
                    style_pressed = new UiStyle_Box { tint = new Color(0, 70, 130, 220) },
                },
            };
            string captured = path;
            btn.on_click = () => CurrentDir_Set(new TDirectory { path = captured });
            _crumbs.Child_Add(btn);
        }

        AddCrumb(root_name, root, PathsEqual(root, _current_dir));
        if (string.IsNullOrEmpty(_current_dir) || !Directory.Exists(_current_dir)) return;

        string rel;
        try { rel = Path.GetRelativePath(root, _current_dir); }
        catch { rel = ""; }
        if (string.IsNullOrEmpty(rel) || rel == "." || rel.StartsWith("..")) return;

        string walk = root;
        string[] parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            walk = Path.Combine(walk, parts[i]);
            AddCrumb(parts[i], walk, i == parts.Length - 1);
        }
    }

    void Grid_Clear()
    {
        if (file_list.scroll_box != null)
            file_list.scroll_box.Child_RemoveAll();
        for (int i = file_list.children.Count - 1; i >= 0; i--)
        {
            if (file_list.children[i] == file_list.scroll_box) continue;
            file_list.children[i].Destroy();
        }
        if (file_list.scroll_box != null) file_list.scroll_box.scroll = 0;
    }

    public void Grid_Refresh()
    {
        Grid_Clear();
        _selected = null;

        if (string.IsNullOrEmpty(_current_dir) || !Directory.Exists(_current_dir))
        {
            _status.text = "0 items";
            return;
        }

        float thumb = Math.Clamp(thumbnail_size, 10, 160);
        float card_w = MathF.Max(72, thumb + 10);
        float card_h = thumb + 40;

        List<EdFileThumbnail> items = new();

        try
        {
            foreach (string dir in Directory.GetDirectories(_current_dir))
            {
                string name = Path.GetFileName(dir);
                if (ShouldSkipName(name)) continue;
                items.Add(MakeThumb(dir, true, null, name, "Folder", new Color(120, 170, 220, 255)));
            }

            foreach (string file in Directory.GetFiles(_current_dir))
            {
                string name = Path.GetFileName(file);
                if (ShouldSkipName(name)) continue;
                string ext = Path.GetExtension(file);

                if (ext.Equals(".ImpAsset", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".ImpScene", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".ImpGame", StringComparison.OrdinalIgnoreCase))
                {
                    ImpAsset asset = ImpAsset.Load(file);
                    if (asset == null)
                    {
                        if (ext.Equals(".ImpScene", StringComparison.OrdinalIgnoreCase))
                            asset = new ImpScene { filepath = file };
                        else
                            asset = new ImpAsset(file);
                    }
                    string label = asset.Editor_GetTypeLabel();
                    string shown = show_file_extensions ? Path.GetFileName(file) : asset.GetName();
                    items.Add(MakeThumb(file, false, asset, shown, label, asset.Editor_GetThumbnail_Color()));
                }
                else if (show_source_files)
                {
                    ImpFile src = ImpFile.GetOrCreate(file);
                    string label = SourceTypeLabel(ext, src);
                    string shown = show_file_extensions ? name : Path.GetFileNameWithoutExtension(name);
                    items.Add(MakeThumb(file, false, src, shown, label, src.Editor_GetThumbnail_Color()));
                }
            }
        }
        catch
        {
            _status.text = "0 items";
            return;
        }

        items.Sort((a, b) =>
        {
            if (a.is_folder != b.is_folder) return a.is_folder ? -1 : 1;
            return string.Compare(a.display_name, b.display_name, StringComparison.OrdinalIgnoreCase);
        });

        string q = _query?.Trim() ?? "";
        string type_filter = "";
        Match tm = Regex.Match(q, @"type\s*==?\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
        if (tm.Success)
        {
            type_filter = tm.Groups[1].Value;
            q = (q.Remove(tm.Index, tm.Length)).Trim();
        }

        int shown_count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            EdFileThumbnail t = items[i];
            if (!PassesFilter(t, q, type_filter)) continue;
            t.size = new Vector2(card_w, card_h);
            t.size_min = new Vector2(card_w, card_h);
            file_list.Child_Add(t);
            shown_count++;
        }

        _status.text = shown_count == 1 ? "1 item" : shown_count + " items";
    }

    EdFileThumbnail MakeThumb(string path, bool folder, I_File file, string name, string type, Color color)
    {
        EdFileThumbnail t = new()
        {
            owner = this,
            path = path,
            is_folder = folder,
            file = file,
            display_name = name,
            type_label = type,
            type_color = color,
        };
        return t;
    }

    static bool PassesFilter(EdFileThumbnail t, string q, string type_filter)
    {
        if (!string.IsNullOrEmpty(type_filter))
        {
            string cmp = (t.type_label ?? "") + " " + t.file?.GetType().Name;
            if (cmp.IndexOf(type_filter, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }
        if (string.IsNullOrEmpty(q)) return true;
        return (t.display_name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
               || (t.type_label ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string SourceTypeLabel(string ext, ImpFile file)
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

    internal void Thumbnail_Select(EdFileThumbnail thumb)
    {
        if (_selected != null) _selected.is_selected = false;
        _selected = thumb;
        if (_selected != null) _selected.is_selected = true;
    }

    internal void Thumbnail_Open(EdFileThumbnail thumb)
    {
        if (thumb == null) return;
        if (thumb.is_folder)
        {
            CurrentDir_Set(new TDirectory { path = thumb.path });
            return;
        }
        thumb.file?.Editor_File_Open();
    }

    internal void Thumbnail_Menu(EdFileThumbnail thumb)
    {
        Vector2 pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
        if (thumb.is_folder)
        {
            C1_PopupMenu.Open(pos, FolderOptions(thumb.path, thumb));
            return;
        }

        List<TPopupMenuOption> opts = thumb.file?.Editor_File_GetOptions() ?? new List<TPopupMenuOption>();
        if (opts.Count == 0)
        {
            opts.Add(new() { text = "Open", on_press = () => Thumbnail_Open(thumb) });
        }
        if (thumb.file is ImpFile src && src.default_asset_type != null)
            opts.Add(new() { text = "Create Asset", on_press = () => { src.Editor_CreateAsset(); Grid_Refresh(); } });
        opts.Add(new() { is_separator = true });
        opts.Add(new() { text = "Rename", on_press = () => Rename_Begin(thumb.path, thumb) });
        opts.Add(new() { text = "Show in Explorer", on_press = () => ShowInExplorer(thumb.path, true) });
        opts.Add(new() { text = "Delete", on_press = () => DeletePath(thumb.path, false) });
        C1_PopupMenu.Open(pos, opts);
    }

    List<TPopupMenuOption> FolderOptions(string path, EdFileThumbnail thumb = null)
    {
        bool fav = _favorite_paths.Exists(p => PathsEqual(p, path));
        return new List<TPopupMenuOption>
        {
            new() { text = "Open", on_press = () => CurrentDir_Set(new TDirectory { path = path }) },
            new() { text = fav ? "Remove from Favorites" : "Add to Favorites", on_press = () => ToggleFavorite(path) },
            new() { is_separator = true },
            new() { text = "New Folder", on_press = NewFolder },
            new() { text = "New Scene", on_press = NewScene },
            new() { is_separator = true },
            new() { text = "Rename", on_press = () => Rename_Begin(path, thumb) },
            new() { text = "Show in Explorer", on_press = () => ShowInExplorer(path, false) },
            new() { text = "Delete", on_press = () => DeletePath(path, true) },
        };
    }

    List<TPopupMenuOption> EmptyFolderOptions()
    {
        return new List<TPopupMenuOption>
        {
            new() { text = "New Folder", on_press = NewFolder },
            new() { text = "New Scene", on_press = NewScene },
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
        RefreshAll();
    }

    void NewScene()
    {
        if (string.IsNullOrEmpty(_current_dir) || !Directory.Exists(_current_dir)) return;
        string dest = UniqueName(_current_dir, "NewScene", ".ImpScene");
        ImpScene scene = new() { filepath = dest };
        scene.File_Write();
        Grid_Refresh();
    }

    void ImportSources()
    {
        if (_selected is { file: ImpFile selected_src })
        {
            selected_src.Editor_CreateAsset();
            Grid_Refresh();
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
        Grid_Refresh();
    }

    void ToggleFavorite(string path)
    {
        int i = _favorite_paths.FindIndex(p => PathsEqual(p, path));
        if (i >= 0) _favorite_paths.RemoveAt(i);
        else _favorite_paths.Add(path);
        RebuildFavorites();
    }

    void RebuildFavorites()
    {
        _fav_list.Child_RemoveAll();
        if (_favorite_paths.Count == 0)
        {
            _fav_list.Child_Add(new C2_Text
            {
                text = "No favorites",
                style = UiStyle_Text.MUTED,
                size = new Vector2(0, 20),
                size_min = new Vector2(0, 20),
                view_alighnment_H = EUIViewportAlignment.Fill,
                text_alignment_h = EUIPositionAlignment.Start,
            });
            return;
        }
        for (int i = 0; i < _favorite_paths.Count; i++)
        {
            string path = _favorite_paths[i];
            C2_Button btn = new()
            {
                text = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                text_style = UiStyle_Text.LIGHT,
                override_font_size = 12,
                content_align_h = EUIPositionAlignment.Start,
                content_pad = 8,
                icon = A_Texture.THUMB_FOLDER,
                icon_size = 14,
                size = new Vector2(0, 22),
                size_min = new Vector2(0, 22),
                view_alighnment_H = EUIViewportAlignment.Fill,
                style = ToolStyle,
            };
            string captured = path;
            btn.on_click = () => CurrentDir_Set(new TDirectory { path = captured });
            _fav_list.Child_Add(btn);
        }
    }

    void DeletePath(string path, bool folder)
    {
        try
        {
            if (folder)
            {
                if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                    Directory.Delete(path);
            }
            else if (File.Exists(path)) File.Delete(path);
        }
        catch { }
        RefreshAll();
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

public class EdFileTree : C2_Tree
{
    public string root_path;
    public Action on_rebuilt;
    string _built_path;

    public void Refresh()
    {
        _built_path = null;
    }

    public override void OnUpdate(double dt)
    {
        if (!string.Equals(_built_path, root_path, StringComparison.OrdinalIgnoreCase))
            RebuildFromDisk();
        base.OnUpdate(dt);
    }

    public void RebuildFromDisk()
    {
        _built_path = root_path;
        Tree_Clear();
        if (string.IsNullOrEmpty(root_path) || !Directory.Exists(root_path)) return;
        TTreeItem root = BuildDir(root_path, true);
        Tree_Add(root);
        Tree_ExpandKey(C2_Tree.KeyOf(root.data, root), true);
        on_rebuilt?.Invoke();
    }

    TTreeItem BuildDir(string path, bool is_root)
    {
        string label = is_root
            ? (string.IsNullOrEmpty(name) ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) : name)
            : Path.GetFileName(path);
        if (string.IsNullOrEmpty(label)) label = path;

        List<TTreeItem> kids = new();
        try
        {
            string[] dirs = Directory.GetDirectories(path);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirs.Length; i++)
            {
                string n = Path.GetFileName(dirs[i]);
                if (PNL_FileBrowser.ShouldSkipName(n)) continue;
                kids.Add(BuildDir(dirs[i], false));
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
                }
            },
            children = kids.ToArray(),
            data = new TDirectory { path = path },
        };
    }
}

// Inline rename box. Like the drag ghost it lives on the popup host, so it draws over the grid,
// is not clipped by the panel, and outlives the refresh its own commit sets off.
public class EdRenameField : C2_Box
{
    public Func<string, bool> is_valid;
    public Action<string> on_commit;

    static EdRenameField _open;

    public static bool IsOpen => _open != null;

    readonly C2_TextEdit _edit = new()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
        style = new UiStyle_TextEdit
        {
            style_background = new UiStyle_Box { tint = new Color(18, 18, 18, 255) },
            text_style = UiStyle_Text.LIGHT,
            placeholder_style = UiStyle_Text.MUTED,
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
            style = new UiStyle_Box { tint = new Color(18, 18, 18, 255) },
            view_alighnment_H = EUIViewportAlignment.Start,
            view_alighnment_V = EUIViewportAlignment.Start,
            size = size,
            size_min = size,
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

    public override void OnDraw2DForeground(double dt, WDrawFlags flags)
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
public class EdDragGhost : ImpComp2D
{
    public string label = "";
    public Color accent = new(120, 170, 220, 255);
    public bool is_valid;

    const float GhostW = 190f;
    const float GhostH = 24f;

    public EdDragGhost()
    {
        cursor_filter = ECursorFilter.Ignore;
        view_alighnment_H = EUIViewportAlignment.Start;
        view_alighnment_V = EUIViewportAlignment.Start;
        size = new Vector2(GhostW, GhostH);
        size_min = size;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        Raylib.DrawRectangleV(dim.position, dim.size, new Color(28, 28, 30, 235));
        Raylib.DrawRectangleLinesEx(
            new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 1.5f,
            is_valid ? new Color(90, 220, 140, 255) : new Color(90, 90, 96, 255));
        Raylib.DrawRectangleV(dim.position + new Vector2(2, 2), new Vector2(3, dim.size.Y - 4), accent);

        UiStyle_Text.LIGHT.Draw(label ?? "", dim.position + new Vector2(10, 0),
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

public class EdFileThumbnail : ImpComp2D
{
    public I_File file;
    public string path;
    public bool is_folder;
    public string display_name;
    public string type_label;
    public Color type_color = new(90, 90, 90, 255);
    public bool is_selected;
    public PNL_FileBrowser owner;

    bool _hover;
    bool _drop_hover;
    double _last_click;

    public EdFileThumbnail()
    {
        cursor_filter = ECursorFilter.Hit;
    }

    public override bool CursorGrab_IsEnabled(ImpPlayer player) => !string.IsNullOrEmpty(path);

    public override object CursorGrab_Payload() => path;

    public override void CursorGrab_Begin(ImpPlayer player)
    {
        base.CursorGrab_Begin(player);
        owner?.Thumbnail_Select(this);
    }

    public override void CursorGrab_DroppedOn(ImpPlayer player, ImpComp dropped)
    {
        base.CursorGrab_DroppedOn(player, dropped);
        _drop_hover = false;
        if (!is_folder || dropped == this) return;
        if (dropped?.CursorGrab_Payload() is string src) owner?.Path_MoveInto(src, path);
    }

    public override void CursorGrab_HoveredAsTarget(ImpPlayer player, ImpComp dropped, bool hovered)
    {
        base.CursorGrab_HoveredAsTarget(player, dropped, hovered);
        _drop_hover = hovered && is_folder && dropped != this
            && dropped?.CursorGrab_Payload() is string src
            && !PNL_FileBrowser.PathsEqual(src, path);
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        Color bg = is_selected ? new Color(0, 72, 128, 255)
            : _hover ? new Color(50, 50, 54, 255)
            : new Color(22, 22, 22, 255);
        Raylib.DrawRectangleV(dim.position, dim.size, bg);
        if (_drop_hover)
            Raylib.DrawRectangleLinesEx(new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 2.5f,
                new Color(90, 220, 140, 255));
        else if (is_selected)
            Raylib.DrawRectangleLinesEx(new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y), 1.5f,
                new Color(0, 156, 227, 255));

        float pad = 4;
        float preview = MathF.Max(24, dim.size.Y - 40);
        Vector2 ppos = dim.position + new Vector2(pad, pad);
        Vector2 psz = new(dim.size.X - pad * 2, preview);

        DrawChecker(ppos, psz, 8);

        Texture2D? tex = null;
        if (is_folder) tex = A_Texture.THUMB_FOLDER?.texture;
        else
        {
            tex = file?.Editor_GetThumbnail_Texture();
            if (tex is not { Id: not 0 })
                tex = A_Texture.THUMB_FILE?.texture;
        }

        if (tex is { Id: not 0, Width: > 0, Height: > 0 } t)
        {
            float scale = MathF.Min(psz.X / t.Width, psz.Y / t.Height);
            float dw = t.Width * scale;
            float dh = t.Height * scale;
            Raylib.DrawTexturePro(t,
                new Rectangle(0, 0, t.Width, t.Height),
                new Rectangle(ppos.X + (psz.X - dw) * 0.5f, ppos.Y + (psz.Y - dh) * 0.5f, dw, dh),
                Vector2.Zero, 0f, Color.White);
        }

        Raylib.DrawRectangleV(new Vector2(ppos.X, ppos.Y + psz.Y - 3), new Vector2(psz.X, 3), type_color);

        float text_y = ppos.Y + psz.Y + 2;
        float text_h = dim.position.Y + dim.size.Y - text_y - 2;
        UiStyle_Text.LIGHT.Draw(display_name ?? "", new Vector2(dim.position.X + 4, text_y),
            new Vector2(dim.size.X - 8, text_h * 0.55f), 12, ETextWrap.None,
            EUIPositionAlignment.Start, EUIPositionAlignment.Start);
        UiStyle_Text.MUTED.Draw(type_label ?? "", new Vector2(dim.position.X + 4, text_y + text_h * 0.5f),
            new Vector2(dim.size.X - 8, text_h * 0.5f), 10, ETextWrap.None,
            EUIPositionAlignment.Start, EUIPositionAlignment.Start);
    }

    static void DrawChecker(Vector2 pos, Vector2 sz, float cell)
    {
        int cols = Math.Max(1, (int)MathF.Ceiling(sz.X / cell));
        int rows = Math.Max(1, (int)MathF.Ceiling(sz.Y / cell));
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            Color c = ((x + y) & 1) == 0 ? new Color(48, 48, 48, 255) : new Color(64, 64, 64, 255);
            float dw = MathF.Min(cell, sz.X - x * cell);
            float dh = MathF.Min(cell, sz.Y - y * cell);
            if (dw <= 0 || dh <= 0) continue;
            Raylib.DrawRectangleV(pos + new Vector2(x * cell, y * cell), new Vector2(dw, dh), c);
        }
    }

    public override void Cursor_OnEnter(ImpPlayer player)
    {
        base.Cursor_OnEnter(player);
        _hover = true;
    }

    public override void Cursor_OnExit(ImpPlayer player)
    {
        base.Cursor_OnExit(player);
        _hover = false;
    }

    public override void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt)
    {
        base.Cursor_OnEvent(player, evnt);
        if (evnt == ECursorEvent.Select_B)
        {
            owner?.Thumbnail_Select(this);
            owner?.Thumbnail_Menu(this);
            return;
        }
        if (evnt != ECursorEvent.Select_A) return;

        double now = Raylib.GetTime();
        bool dbl = now - _last_click < 0.35;
        _last_click = now;
        owner?.Thumbnail_Select(this);
        if (dbl) owner?.Thumbnail_Open(this);
    }
}
