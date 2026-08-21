using System.Numerics;
using Editor.Panel;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Dialogs;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Dialog;

public class DLG_SaveFile : ImpDialog
{
    public C2_Box box = new();
    public EdFileTree directory_tree = new();
    public C2_TextEdit txt_name = new();
    public C2_Button btn_save = new();
    public C2_Button btn_cancel = new();

    ImpAsset _asset;
    Action<string> _on_save;
    string _suggested_folder;
    string _folder = "";
    string _extension = "ImpAsset";
    C2_Text _title;
    C2_Text _lbl_folder;
    C2_Text _lbl_ext;
    C2_Text _hint;
    bool _ui;

    public static void Run(object file, Action<string> on_save, string folder = null)
    {
        DLG_SaveFile dlg = new();
        dlg._asset = file as ImpAsset;
        dlg._on_save = on_save;
        dlg._suggested_folder = folder;
        dlg.Show();
    }

    public DLG_SaveFile()
    {
        box.style = new UI_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(520, 560),
            size_min = new Vector2(420, 420),
        };

        directory_tree.name = "Content";
        directory_tree.layout = TLayout2.FULL;
        directory_tree.on_item_click = item =>
        {
            if (item.data is not TDirectory dir)
            {
                return;
            }
            if (string.IsNullOrEmpty(dir.path))
            {
                return;
            }
            _folder = dir.path;
            FolderLabel_Set();
            if (_hint != null)
            {
                _hint.text = "";
            }
        };

        btn_save.text = "Save";
        btn_save.on_click = Confirm;
        btn_cancel.text = "Cancel";
        btn_cancel.on_click = Cancel;
        txt_name.on_submit = _ => Confirm();
    }

    public override void Show()
    {
        EnsureUi();
        FillFromAsset();
        on_dismiss = Cancel;
        base.Show();
        Overlay?.Child_Add(box);
        txt_name.is_focused = true;
        txt_name.cursor = (txt_name.text ?? "").Length;
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].input_hog = txt_name;
        }
    }

    void FillFromAsset()
    {
        if (_asset != null)
        {
            _extension = _asset.File_GetExtension();
        }
        if (string.IsNullOrEmpty(_extension))
        {
            _extension = "ImpAsset";
        }

        string root = ImpFile.ContentDir_Game();
        if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
        {
            try
            {
                Directory.CreateDirectory(root);
            }
            catch
            {
            }
        }

        string folder = _suggested_folder;
        if (string.IsNullOrEmpty(folder) && _asset != null && _asset.File_CanWrite())
        {
            folder = Path.GetDirectoryName(ImpFile.Path_Resolve(_asset.filepath));
        }
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            folder = root;
        }
        if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(folder))
        {
            if (!PNL_FileBrowser.IsUnder(folder, root) && !PNL_FileBrowser.PathsEqual(folder, root))
            {
                folder = root;
            }
        }
        _folder = folder ?? "";

        directory_tree.root_path = root;
        directory_tree.RebuildFromDisk();
        if (!string.IsNullOrEmpty(_folder))
        {
            string acc = root;
            directory_tree.Tree_ExpandKey(root, true);
            string full = "";
            string root_full = "";
            try
            {
                full = Path.GetFullPath(_folder);
                root_full = Path.GetFullPath(root);
            }
            catch
            {
            }
            if (!string.IsNullOrEmpty(full) && !string.IsNullOrEmpty(root_full) && full.Length > root_full.Length)
            {
                string rel = full.Substring(root_full.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string[] parts = rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    acc = Path.Combine(acc, parts[i]);
                    directory_tree.Tree_ExpandKey(acc, true);
                }
            }
            directory_tree.Tree_SelectData(new TDirectory { path = _folder });
        }

        string name = "Untitled";
        if (_asset != null)
        {
            if (_asset.File_CanWrite())
            {
                name = ImpAsset.Name_ForPath(_asset.filepath);
            }
            else
            {
                name = _asset.GetName();
                if (name.EndsWith("*"))
                {
                    name = name.Substring(0, name.Length - 1);
                }
            }
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Untitled";
        }
        txt_name.text = name;
        txt_name.cursor = name.Length;

        if (_title != null)
        {
            string kind = "File";
            if (_asset != null)
            {
                kind = _asset.Editor_GetTypeLabel();
            }
            _title.text = "Save " + kind + " As";
        }
        if (_lbl_ext != null)
        {
            _lbl_ext.text = "." + _extension;
        }
        FolderLabel_Set();
        if (_hint != null)
        {
            _hint.text = "";
        }
    }

    void FolderLabel_Set()
    {
        if (_lbl_folder == null)
        {
            return;
        }
        string text = _folder ?? "";
        string root = ImpFile.ContentDir_Game();
        try
        {
            if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(root))
            {
                string full = Path.GetFullPath(text);
                string root_full = Path.GetFullPath(root);
                if (PNL_FileBrowser.PathsEqual(full, root_full))
                {
                    text = "Content";
                }
                else if (PNL_FileBrowser.IsUnder(full, root_full))
                {
                    text = "Content/" + full.Substring(root_full.Length).TrimStart('\\', '/').Replace('\\', '/');
                }
            }
        }
        catch
        {
        }
        if (string.IsNullOrEmpty(text))
        {
            text = "Folder";
        }
        _lbl_folder.text = text;
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

        _title = new C2_Text
        {
            text = "Save As",
            style = UI_Text.LIGHT,
            layout = new TLayout2
            {
                size = new Vector2(0, 26),
                size_min = new Vector2(0, 26),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        C2_Text lbl_where = new()
        {
            text = "Save in",
            style = UI_Text.MUTED,
            layout = new TLayout2
            {
                size = new Vector2(0, 16),
                size_min = new Vector2(0, 16),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        _lbl_folder = new C2_Text
        {
            text = "Content",
            style = UI_Text.MUTED,
            layout = new TLayout2
            {
                size = new Vector2(0, 16),
                size_min = new Vector2(0, 16),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        C2_Text lbl_name = new()
        {
            text = "Name",
            style = UI_Text.MUTED,
            layout = new TLayout2
            {
                size = new Vector2(0, 16),
                size_min = new Vector2(0, 16),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        C2_List name_row = new()
        {
            orentation = EUIOrentation.H,
            spacing = 6,
            layout = new TLayout2
            {
                size = new Vector2(0, 26),
                size_min = new Vector2(0, 26),
                orient_H = EUIViewportAlignment.Fill,
            },
        };
        txt_name.layout = new TLayout2
        {
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
            orient_H = EUIViewportAlignment.Fill,
        };
        _lbl_ext = new C2_Text
        {
            text = ".ImpAsset",
            style = UI_Text.MUTED,
            text_alignment_v = EUIPositionAlignment.Center,
            layout = new TLayout2
            {
                size = new Vector2(90, 26),
                size_min = new Vector2(80, 26),
            },
        };
        name_row.Child_Add(txt_name);
        name_row.Child_Add(_lbl_ext);

        _hint = new C2_Text
        {
            text = "",
            style = UI_Text.MUTED,
            layout = new TLayout2
            {
                size = new Vector2(0, 18),
                size_min = new Vector2(0, 18),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        btn_cancel.layout = new TLayout2
        {
            size = new Vector2(90, 28),
            size_min = new Vector2(80, 28),
        };
        btn_save.layout = new TLayout2
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
        btns.Child_Add(btn_save);

        col.Child_Add(_title);
        col.Child_Add(lbl_where);
        col.Child_Add(directory_tree);
        col.Child_Add(_lbl_folder);
        col.Child_Add(lbl_name);
        col.Child_Add(name_row);
        col.Child_Add(_hint);
        col.Child_Add(btns);
        box.Child_Add(col);
    }

    void Confirm()
    {
        string folder = (_folder ?? "").Trim();
        string name = (txt_name.text ?? "").Trim();
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name))
        {
            if (_hint != null)
            {
                _hint.text = "Folder and name are required.";
            }
            return;
        }

        char[] bad = Path.GetInvalidFileNameChars();
        for (int i = 0; i < name.Length; i++)
        {
            if (Array.IndexOf(bad, name[i]) >= 0)
            {
                if (_hint != null)
                {
                    _hint.text = "Name has invalid characters.";
                }
                return;
            }
        }

        string ext = "." + _extension;
        if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            name += ext;
        }

        string path;
        try
        {
            path = Path.GetFullPath(Path.Combine(folder, name));
        }
        catch
        {
            if (_hint != null)
            {
                _hint.text = "Path is not valid.";
            }
            return;
        }

        string dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
        {
            if (_hint != null)
            {
                _hint.text = "Folder is not valid.";
            }
            return;
        }

        bool same = false;
        if (_asset != null && _asset.File_CanWrite())
        {
            try
            {
                same = string.Equals(
                    Path.GetFullPath(ImpFile.Path_Resolve(_asset.filepath)),
                    path,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                same = false;
            }
        }

        if (File.Exists(path) && !same)
        {
            Action<string> save = _on_save;
            Close();
            Dialog_Confirm.Run("Overwrite " + Path.GetFileName(path) + "?", () =>
            {
                save?.Invoke(path);
            });
            return;
        }

        _on_save?.Invoke(path);
        Close();
    }

    void Cancel()
    {
        if (is_open)
        {
            Close();
        }
    }
}
