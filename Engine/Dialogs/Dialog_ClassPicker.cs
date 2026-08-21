using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Dialogs;

public class Dialog_ClassPicker : ImpDialog
{
    public C2_Box box = new();
    public C2_SearchBar search_bar = new();
    public C2_Tree class_tree = new();
    public C2_Text class_name = new();

    public C2_TextEdit txtedit_create_name = new();

    public bool is_create_new = false;

    public C2_Button btn_confirm = new();
    public C2_Button btn_cancel = new();

    public Type root_type;
    public Type selected_type;
    public Type current_type;
    public bool allow_none;
    public bool none_selected;

    public string title = "Choose Class";

    public Action<Dialog_ClassPicker> on_confirm;
    public Action<Dialog_ClassPicker> on_cancel;

    static readonly object NoneData = "";

    // Create callbacks set this to keep the picker open (empty name, name already exists, ...).
    public bool stay_open;

    C2_Text _title_lbl;
    C2_Text _name_lbl;
    C2_Text _hint;
    bool _ui;

    public static void Run(Type root_type, Action<Type> on_picked, Action on_cancel = null,
        string title = null, Type current = null, bool allow_none = false)
    {
        Dialog_ClassPicker dlg = new();
        dlg.root_type = root_type;
        dlg.current_type = current;
        dlg.allow_none = allow_none;
        if (!string.IsNullOrEmpty(title))
        {
            dlg.title = title;
        }
        dlg.on_confirm = d =>
        {
            if (d.selected_type != null)
            {
                on_picked?.Invoke(d.selected_type);
                return;
            }
            if (d.allow_none && d.none_selected)
            {
                on_picked?.Invoke(null);
            }
        };
        dlg.on_cancel = _ => on_cancel?.Invoke();
        dlg.Show();
    }

    public Dialog_ClassPicker()
    {
        box.style = new UI_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(420, 540),
            size_min = new Vector2(360, 400),
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
            class_tree.Tree_FilterClasses(q);
            InsertNone();
        };

        class_tree.layout = TLayout2.FULL;
        class_tree.on_item_click = item =>
        {
            if (IsNoneItem(item))
            {
                none_selected = true;
                selected_type = null;
                class_name.text = "None";
                Hint_Set("");
                return;
            }
            if (item.is_disabled || item.data is not Type t || t.IsAbstract)
            {
                return;
            }
            none_selected = false;
            selected_type = t;
            class_name.text = C2_Tree.Class_DisplayName(t);
            Hint_Set("");
            if (is_create_new)
            {
                FocusCreateName();
            }
        };
        class_tree.on_item_double_click = item =>
        {
            if (IsNoneItem(item))
            {
                none_selected = true;
                selected_type = null;
                class_name.text = "None";
                Confirm();
                return;
            }
            if (item.is_disabled || item.data is not Type t || t.IsAbstract)
            {
                return;
            }
            none_selected = false;
            selected_type = t;
            class_name.text = C2_Tree.Class_DisplayName(t);
            Confirm();
        };

        txtedit_create_name.text_placeholder = "Name";
        txtedit_create_name.on_submit = _ => Confirm();

        class_name.style = UI_Text.MUTED;
        class_name.layout = new TLayout2
        {
            size = new Vector2(0, 20),
            size_min = new Vector2(0, 20),
            orient_H = EUIViewportAlignment.Fill,
        };

        btn_confirm.text = "OK";
        btn_confirm.on_click = Confirm;
        btn_cancel.text = "Cancel";
        btn_cancel.on_click = Cancel;
    }

    public void BuildRootType(Type type, Action<Type> filter = null)
    {
        root_type = type;
        BuildTree();
    }

    public void BuildTree()
    {
        class_tree.Tree_Populate_FromClasses(root_type ?? typeof(ImpComp));
        class_tree.Tree_ExpandAll(true);
    }

    public override void Show()
    {
        selected_type = null;
        none_selected = false;
        class_name.text = "";
        stay_open = false;
        txtedit_create_name.text = "";
        txtedit_create_name.cursor = 0;
        if (search_bar.text_edit != null)
        {
            search_bar.text_edit.text = "";
        }
        BuildTree();
        InsertNone();
        if (current_type != null)
        {
            selected_type = current_type;
            none_selected = false;
            class_name.text = C2_Tree.Class_DisplayName(current_type);
            class_tree.Tree_SelectData(current_type);
        }
        else if (allow_none && !is_create_new)
        {
            none_selected = true;
            class_name.text = "None";
            class_tree.Tree_SelectData(NoneData);
        }
        EnsureUi();
        if (_title_lbl != null)
        {
            _title_lbl.text = title ?? "Choose Class";
        }
        txtedit_create_name.is_visible = is_create_new;
        if (_name_lbl != null)
        {
            _name_lbl.is_visible = is_create_new;
        }
        if (_hint != null)
        {
            _hint.is_visible = is_create_new;
            _hint.text = "";
        }
        if (is_create_new)
        {
            btn_confirm.text = "Create";
        }
        else
        {
            btn_confirm.text = "OK";
        }
        on_dismiss = Cancel;
        base.Show();
        Overlay?.Child_Add(box);
        if (is_create_new)
        {
            FocusCreateName();
        }
    }

    void EnsureUi()
    {
        if (_ui) return;
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
            text = title ?? "Choose Class",
            style = UI_Text.LIGHT,
            layout = new TLayout2
            {
                size = new Vector2(0, 26),
                size_min = new Vector2(0, 26),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        _name_lbl = new C2_Text
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

        txtedit_create_name.layout = new TLayout2
        {
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
            orient_H = EUIViewportAlignment.Fill,
        };

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
        col.Child_Add(class_name);
        col.Child_Add(class_tree);
        col.Child_Add(_name_lbl);
        col.Child_Add(txtedit_create_name);
        col.Child_Add(_hint);
        col.Child_Add(btns);
        box.Child_Add(col);
    }

    public void Hint_Set(string text)
    {
        if (_hint != null)
        {
            _hint.text = text ?? "";
        }
    }

    void FocusCreateName()
    {
        txtedit_create_name.is_focused = true;
        txtedit_create_name.cursor = (txtedit_create_name.text ?? "").Length;
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].input_hog = txtedit_create_name;
        }
    }

    string CreateName_Get()
    {
        return (txtedit_create_name.text ?? "").Trim();
    }

    bool CreateName_IsValid()
    {
        string name = CreateName_Get();
        if (string.IsNullOrEmpty(name))
        {
            Hint_Set("Name is required.");
            FocusCreateName();
            return false;
        }

        char[] bad = Path.GetInvalidFileNameChars();
        for (int i = 0; i < name.Length; i++)
        {
            if (Array.IndexOf(bad, name[i]) >= 0)
            {
                Hint_Set("Name has invalid characters.");
                FocusCreateName();
                return false;
            }
        }

        txtedit_create_name.text = name;
        txtedit_create_name.cursor = name.Length;
        Hint_Set("");
        return true;
    }

    void InsertNone()
    {
        if (!allow_none || is_create_new)
        {
            return;
        }
        class_tree.Tree_AddAt(0, new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection { text = "None" },
            },
            data = NoneData,
        });
        if (none_selected)
        {
            class_tree.Tree_SelectData(NoneData);
        }
    }

    static bool IsNoneItem(TTreeItem item)
    {
        if (item.data == NoneData)
        {
            return true;
        }
        if (item.data is string s && s.Length == 0)
        {
            return true;
        }
        return false;
    }

    void Confirm()
    {
        if (allow_none && none_selected && !is_create_new)
        {
            stay_open = false;
            on_confirm?.Invoke(this);
            if (is_open && !stay_open)
            {
                Close();
            }
            return;
        }
        if (selected_type == null || selected_type.IsAbstract)
        {
            if (is_create_new)
            {
                Hint_Set("Pick a class.");
            }
            return;
        }
        if (is_create_new && !CreateName_IsValid())
        {
            return;
        }

        stay_open = false;
        on_confirm?.Invoke(this);
        if (is_open && !stay_open)
        {
            Close();
        }
    }

    void Cancel()
    {
        on_cancel?.Invoke(this);
        if (is_open) Close();
    }
}
