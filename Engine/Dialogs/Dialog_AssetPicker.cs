using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Files;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Dialogs;

public class Dialog_AssetPicker : ImpDialog
{
    public C2_Box box = new();
    public C2_SearchBar search_bar = new();
    public C2_Tree asset_tree = new();
    public C2_Text selected_name = new();
    public C2_Text selected_path = new();
    public C2_Button btn_confirm = new();
    public C2_Button btn_cancel = new();

    public Type asset_type = typeof(ImpAsset);
    public string title = "Select Asset";
    public string current_path = "";
    public bool allow_none = true;
    public string selected_path_value = "";
    public bool none_selected;

    public Action<Dialog_AssetPicker> on_confirm;
    public Action<Dialog_AssetPicker> on_cancel;

    static readonly object NoneData = "";

    C2_Text _title_lbl;
    bool _ui;
    string _query = "";
    List<TAssetEntry> _entries = new();

    class TAssetEntry
    {
        public string path;
        public string name;
        public string cls;
        public string group;
        public string rel;
        public Type type;
    }

    class TFolder
    {
        public string name;
        public string key;
        public List<TFolder> folders = new();
        public List<TAssetEntry> files = new();

        public TFolder Child(string name, string key)
        {
            for (int i = 0; i < folders.Count; i++)
            {
                if (string.Equals(folders[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return folders[i];
                }
            }
            TFolder made = new() { name = name, key = key };
            folders.Add(made);
            return made;
        }
    }

    public static void Run(Type asset_type, Action<string> on_picked, Action on_cancel = null,
        string current_path = null, string title = null, bool allow_none = true)
    {
        Dialog_AssetPicker dlg = new();
        dlg.asset_type = asset_type ?? typeof(ImpAsset);
        dlg.current_path = current_path ?? "";
        dlg.allow_none = allow_none;
        if (!string.IsNullOrEmpty(title))
        {
            dlg.title = title;
        }
        else
        {
            dlg.title = "Select " + C2_Tree.Class_DisplayName(dlg.asset_type);
        }
        dlg.on_confirm = d =>
        {
            if (d.none_selected)
            {
                on_picked?.Invoke("");
                return;
            }
            if (!string.IsNullOrEmpty(d.selected_path_value))
            {
                on_picked?.Invoke(d.selected_path_value);
            }
        };
        dlg.on_cancel = _ => on_cancel?.Invoke();
        dlg.Show();
    }

    public Dialog_AssetPicker()
    {
        box.style = new UI_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(560, 560),
            size_min = new Vector2(420, 420),
        };

        search_bar.placeholder = "Search";
        search_bar.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
        };
        search_bar.on_search = q =>
        {
            _query = q ?? "";
            BuildTree();
        };

        asset_tree.layout = TLayout2.FULL;
        asset_tree.on_item_click = item =>
        {
            SelectItem(item, false);
        };
        asset_tree.on_item_double_click = item =>
        {
            SelectItem(item, true);
        };

        selected_name.style = UI_Text.LIGHT;
        selected_name.layout = new TLayout2
        {
            size = new Vector2(0, 20),
            size_min = new Vector2(0, 20),
            orient_H = EUIViewportAlignment.Fill,
        };

        selected_path.style = UI_Text.MUTED;
        selected_path.wrap = ETextWrap.None;
        selected_path.layout = new TLayout2
        {
            size = new Vector2(0, 18),
            size_min = new Vector2(0, 18),
            orient_H = EUIViewportAlignment.Fill,
        };

        btn_confirm.text = "OK";
        btn_confirm.on_click = Confirm;
        btn_cancel.text = "Cancel";
        btn_cancel.on_click = Cancel;
    }

    public override void Show()
    {
        _query = "";
        none_selected = false;
        selected_path_value = "";
        if (search_bar.text_edit != null)
        {
            search_bar.text_edit.text = "";
        }
        CollectEntries();
        EnsureUi();
        if (_title_lbl != null)
        {
            _title_lbl.text = title ?? "Select Asset";
        }
        Label_Set("", "");
        BuildTree();
        SelectCurrent();
        on_dismiss = Cancel;
        base.Show();
        Overlay?.Child_Add(box);
        FocusSearch();
    }

    void EnsureUi()
    {
        if (_ui)
        {
            return;
        }
        _ui = true;

        C2_List col = new()
        {
            orentation = EUIOrentation.V,
            is_scrollable = false,
            spacing = 4,
            layout = TLayout2.FULL,
        };

        _title_lbl = new C2_Text
        {
            text = title ?? "Select Asset",
            style = UI_Text.LIGHT,
            layout = new TLayout2
            {
                size = new Vector2(0, 26),
                size_min = new Vector2(0, 26),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        btn_cancel.layout = new TLayout2
        {
            size = new Vector2(90, 28),
            size_min = new Vector2(80, 28),
        };
        btn_confirm.layout = new TLayout2
        {
            size = new Vector2(90, 28),
            size_min = new Vector2(80, 28),
        };

        C2_List btns = new()
        {
            orentation = EUIOrentation.H,
            spacing = 8,
            layout = new TLayout2
            {
                size = new Vector2(0, 30),
                size_min = new Vector2(0, 30),
                orient_H = EUIViewportAlignment.Fill,
            },
        };
        btns.Child_Add(btn_cancel);
        btns.Child_Add(btn_confirm);

        col.Child_Add(_title_lbl);
        col.Child_Add(search_bar);
        col.Child_Add(selected_name);
        col.Child_Add(asset_tree);
        col.Child_Add(selected_path);
        col.Child_Add(btns);
        box.Child_Add(col);
    }

    void FocusSearch()
    {
        if (search_bar.text_edit == null)
        {
            return;
        }
        search_bar.text_edit.is_focused = true;
        search_bar.text_edit.cursor = (search_bar.text_edit.text ?? "").Length;
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].input_hog = search_bar.text_edit;
        }
    }

    void CollectEntries()
    {
        _entries.Clear();
        Type want = asset_type ?? typeof(ImpAsset);
        string game = ImpFile.ContentDir_Game();
        string engine = ImpFile.ContentDir_Engine();
        bool same = PathsEqual(game, engine);

        foreach (var (path, cls) in ImpAsset.Files_OfType(want))
        {
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch
            {
                full = path;
            }

            string group = "Content";
            string rel = Path.GetFileName(full);
            if (!same && IsUnder(full, game))
            {
                group = "Game";
                rel = RelPath(game, full);
            }
            else if (!same && IsUnder(full, engine))
            {
                group = "Engine";
                rel = RelPath(engine, full);
            }
            else if (!string.IsNullOrEmpty(game) && IsUnder(full, game))
            {
                group = "Content";
                rel = RelPath(game, full);
            }
            else if (!string.IsNullOrEmpty(engine) && IsUnder(full, engine))
            {
                group = "Content";
                rel = RelPath(engine, full);
            }

            Type found = ImpAsset.AssetType_FromName(cls);
            _entries.Add(new TAssetEntry
            {
                path = StorePath(path),
                name = ImpAsset.Name_ForPath(path),
                cls = cls ?? "",
                group = group,
                rel = rel.Replace('\\', '/'),
                type = found ?? want,
            });
        }

        foreach (var (key, asset) in ImpAsset.Builtins_OfType(want))
        {
            Type found = asset != null ? asset.GetType() : want;
            _entries.Add(new TAssetEntry
            {
                path = ImpAsset.BuiltinPrefix + key,
                name = ImpAsset.Name_ForPath(ImpAsset.BuiltinPrefix + key),
                cls = found.Name,
                group = "Builtins",
                rel = key.Replace('\\', '/'),
                type = found,
            });
        }

        _entries.Sort((a, b) =>
        {
            int g = string.Compare(a.group, b.group, StringComparison.OrdinalIgnoreCase);
            if (g != 0)
            {
                return g;
            }
            return string.Compare(a.rel, b.rel, StringComparison.OrdinalIgnoreCase);
        });
    }

    void BuildTree()
    {
        string q = _query != null ? _query.Trim() : "";
        bool searching = q.Length > 0;
        List<TTreeItem> items = new();

        if (allow_none && (!searching || "none".Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(Item_None());
        }

        if (searching)
        {
            foreach (TAssetEntry e in _entries)
            {
                if (!Entry_Matches(e, q))
                {
                    continue;
                }
                items.Add(Item_File(e, true));
            }
        }
        else
        {
            TFolder root = new() { name = "", key = "root" };
            foreach (TAssetEntry e in _entries)
            {
                TFolder group = root.Child(e.group, e.group);
                string[] parts = e.rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
                TFolder cur = group;
                string acc = e.group;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    acc = acc + "/" + parts[i];
                    cur = cur.Child(parts[i], acc);
                }
                cur.files.Add(e);
            }
            SortFolder(root);
            for (int i = 0; i < root.folders.Count; i++)
            {
                items.Add(Item_Folder(root.folders[i]));
            }
        }

        object keep = asset_tree.selected_data;
        asset_tree.Tree_SetItems(items);
        if (searching)
        {
            asset_tree.Tree_ExpandAll(true);
        }
        else
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].data is TDirectory dir && !string.IsNullOrEmpty(dir.path))
                {
                    asset_tree.Tree_ExpandKey(dir.path, true);
                }
            }
            ExpandToPath(current_path);
        }
        if (keep != null)
        {
            asset_tree.Tree_SelectData(keep);
        }
    }

    void ExpandToPath(string path)
    {
        string want = StorePath(path);
        if (string.IsNullOrEmpty(want))
        {
            return;
        }
        TAssetEntry found = null;
        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].path, want, StringComparison.OrdinalIgnoreCase))
            {
                found = _entries[i];
                break;
            }
        }
        if (found == null)
        {
            return;
        }
        asset_tree.Tree_ExpandKey(found.group, true);
        string[] parts = found.rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string acc = found.group;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            acc = acc + "/" + parts[i];
            asset_tree.Tree_ExpandKey(acc, true);
        }
    }

    static void SortFolder(TFolder folder)
    {
        folder.folders.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        folder.files.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < folder.folders.Count; i++)
        {
            SortFolder(folder.folders[i]);
        }
    }

    static bool Entry_Matches(TAssetEntry e, string q)
    {
        if (e.name != null && e.name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (e.cls != null && e.cls.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (e.path != null && e.path.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (e.rel != null && e.rel.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    static TTreeItem Item_None()
    {
        return new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection { text = "None" },
            },
            data = NoneData,
        };
    }

    static TTreeItem Item_File(TAssetEntry e, bool show_path)
    {
        List<TTreeItemSection> sections = new()
        {
            new TTreeItemSection
            {
                text = e.name,
                icon = C2_Tree.Class_Icon(e.type) ?? A_Texture.THUMB_FILE,
            },
        };
        if (!string.IsNullOrEmpty(e.cls))
        {
            sections.Add(new TTreeItemSection
            {
                text = C2_Tree.Class_DisplayName(e.type),
                color = new Color(160, 160, 160, 255),
            });
        }
        if (show_path && !string.IsNullOrEmpty(e.path))
        {
            sections.Add(new TTreeItemSection
            {
                text = e.path,
                color = new Color(130, 130, 130, 255),
            });
        }
        return new TTreeItem
        {
            sections = sections.ToArray(),
            data = new TFile { path = e.path },
        };
    }

    static TTreeItem Item_Folder(TFolder folder)
    {
        List<TTreeItem> kids = new();
        for (int i = 0; i < folder.folders.Count; i++)
        {
            kids.Add(Item_Folder(folder.folders[i]));
        }
        for (int i = 0; i < folder.files.Count; i++)
        {
            kids.Add(Item_File(folder.files[i], false));
        }
        return new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection
                {
                    text = folder.name,
                    icon = A_Texture.THUMB_FOLDER,
                },
            },
            data = new TDirectory { path = folder.key },
            children = kids.ToArray(),
            is_disabled = true,
        };
    }

    void SelectItem(TTreeItem item, bool confirm)
    {
        if (item.is_disabled)
        {
            return;
        }
        if (IsNone(item.data))
        {
            none_selected = true;
            selected_path_value = "";
            Label_Set("None", "");
            if (confirm)
            {
                Confirm();
            }
            return;
        }
        if (item.data is TFile file && !string.IsNullOrEmpty(file.path))
        {
            none_selected = false;
            selected_path_value = file.path;
            string pretty = ImpAsset.Name_ForPath(file.path);
            Type t = TypeOf(file.path);
            if (t != null)
            {
                pretty = pretty + "  (" + C2_Tree.Class_DisplayName(t) + ")";
            }
            Label_Set(pretty, file.path);
            if (confirm)
            {
                Confirm();
            }
        }
    }

    Type TypeOf(string path)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].path, path, StringComparison.OrdinalIgnoreCase))
            {
                return _entries[i].type;
            }
        }
        return asset_type;
    }

    void SelectCurrent()
    {
        string want = StorePath(current_path);
        if (string.IsNullOrEmpty(want))
        {
            if (allow_none)
            {
                none_selected = true;
                selected_path_value = "";
                Label_Set("None", "");
                asset_tree.Tree_SelectData(NoneData);
            }
            return;
        }

        none_selected = false;
        selected_path_value = want;
        string pretty = ImpAsset.Name_ForPath(want);
        Type t = TypeOf(want);
        if (t != null)
        {
            pretty = pretty + "  (" + C2_Tree.Class_DisplayName(t) + ")";
        }
        Label_Set(pretty, want);
        asset_tree.Tree_SelectData(new TFile { path = want });
    }

    void Label_Set(string name, string path)
    {
        selected_name.text = name ?? "";
        selected_path.text = path ?? "";
    }

    static bool IsNone(object data)
    {
        if (data == NoneData)
        {
            return true;
        }
        if (data is string s && s.Length == 0)
        {
            return true;
        }
        return false;
    }

    static string StorePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }
        if (ImpAsset.Path_IsBuiltin(path))
        {
            return path.Replace('\\', '/');
        }
        return File_JSON.Path_Tokenize(path);
    }

    static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
        {
            return false;
        }
        try
        {
            string a = Path.GetFullPath(path);
            string b = Path.GetFullPath(root);
            if (!b.EndsWith(Path.DirectorySeparatorChar))
            {
                b += Path.DirectorySeparatorChar;
            }
            return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
                || string.Equals(a, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return false;
        }
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    static string RelPath(string root, string full)
    {
        try
        {
            return Path.GetRelativePath(root, full);
        }
        catch
        {
            return Path.GetFileName(full);
        }
    }

    void Confirm()
    {
        if (!none_selected && string.IsNullOrEmpty(selected_path_value))
        {
            return;
        }
        on_confirm?.Invoke(this);
        if (is_open)
        {
            Close();
        }
    }

    void Cancel()
    {
        on_cancel?.Invoke(this);
        if (is_open)
        {
            Close();
        }
    }
}
