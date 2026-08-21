using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Dialogs;

public class Dialog_CompPicker : ImpDialog
{
    public C2_Box box = new();
    public C2_SearchBar search_bar = new();
    public C2_Tree comp_tree = new();
    public C2_Text selected_name = new();
    public C2_Button btn_confirm = new();
    public C2_Button btn_cancel = new();

    public Type accepted_type = typeof(ImpComp);
    public ImpScene scene;
    public ImpComp current;
    public ImpComp selected;
    public bool allow_none = true;
    public bool none_selected;
    public Func<ImpComp, bool> filter;
    public string title = "Select Component";

    public Action<Dialog_CompPicker> on_confirm;
    public Action<Dialog_CompPicker> on_cancel;

    static readonly object NoneData = "";

    C2_Text _title_lbl;
    bool _ui;
    string _query = "";

    public static void Run(Type accepted_type, Action<ImpComp> on_picked, Action on_cancel = null,
        ImpScene scene = null, ImpComp current = null, string title = null, bool allow_none = true,
        Func<ImpComp, bool> filter = null)
    {
        Dialog_CompPicker dlg = new();
        dlg.accepted_type = accepted_type ?? typeof(ImpComp);
        dlg.scene = scene;
        dlg.current = current;
        dlg.allow_none = allow_none;
        dlg.filter = filter;
        if (!string.IsNullOrEmpty(title))
        {
            dlg.title = title;
        }
        else
        {
            dlg.title = "Select " + C2_Tree.Class_DisplayName(dlg.accepted_type);
        }
        dlg.on_confirm = d =>
        {
            if (d.none_selected)
            {
                on_picked?.Invoke(null);
                return;
            }
            on_picked?.Invoke(d.selected);
        };
        dlg.on_cancel = _ => on_cancel?.Invoke();
        dlg.Show();
    }

    public Dialog_CompPicker()
    {
        box.style = new UI_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(460, 540),
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
            _query = q ?? "";
            BuildTree();
        };

        comp_tree.layout = TLayout2.FULL;
        comp_tree.on_item_click = item =>
        {
            SelectItem(item, false);
        };
        comp_tree.on_item_double_click = item =>
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

        btn_confirm.text = "OK";
        btn_confirm.on_click = Confirm;
        btn_cancel.text = "Cancel";
        btn_cancel.on_click = Cancel;
    }

    public override void Show()
    {
        _query = "";
        none_selected = false;
        selected = null;
        if (search_bar.text_edit != null)
        {
            search_bar.text_edit.text = "";
        }
        if (scene == null)
        {
            if (current != null && current.scene != null)
            {
                scene = current.scene;
            }
            else
            {
                scene = ImpScene.current;
            }
        }
        EnsureUi();
        if (_title_lbl != null)
        {
            _title_lbl.text = title ?? "Select Component";
        }
        Label_Set("");
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
            text = title ?? "Select Component",
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
        col.Child_Add(comp_tree);
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

    void BuildTree()
    {
        string q = _query != null ? _query.Trim() : "";
        bool searching = q.Length > 0;
        List<TTreeItem> items = new();

        if (allow_none && (!searching || "none".Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(Item_None());
        }

        ImpComp root = scene != null ? scene.root : null;
        if (root != null)
        {
            if (Item_FromComp(root, q, searching, out TTreeItem item))
            {
                items.Add(item);
            }
        }

        object keep = comp_tree.selected_data;
        comp_tree.Tree_SetItems(items);
        comp_tree.Tree_ExpandAll(true);
        if (keep != null)
        {
            comp_tree.Tree_SelectData(keep);
        }
    }

    bool Item_FromComp(ImpComp c, string q, bool searching, out TTreeItem item)
    {
        item = default;
        if (c == null)
        {
            return false;
        }

        List<TTreeItem> kids = new();
        for (int i = 0; i < c.children.Count; i++)
        {
            ImpComp child = c.children[i];
            if (child == null)
            {
                continue;
            }
            if (Item_FromComp(child, q, searching, out TTreeItem kid))
            {
                kids.Add(kid);
            }
        }

        bool self_match = !searching || Comp_Matches(c, q);
        if (searching && !self_match && kids.Count == 0)
        {
            return false;
        }

        bool accepted = IsAccepted(c);
        string label = Comp_Label(c);
        item = new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection
                {
                    text = label,
                    icon = C2_Tree.Class_Icon(c.GetType()),
                    color = C2_Tree.Comp_Tint(c),
                }
            },
            children = kids.ToArray(),
            data = c,
            is_disabled = !accepted,
        };
        return true;
    }

    bool IsAccepted(ImpComp c)
    {
        if (c == null)
        {
            return false;
        }
        Type want = accepted_type ?? typeof(ImpComp);
        if (!want.IsAssignableFrom(c.GetType()))
        {
            return false;
        }
        if (filter != null && !filter(c))
        {
            return false;
        }
        return true;
    }

    static bool Comp_Matches(ImpComp c, string q)
    {
        if (c == null || string.IsNullOrEmpty(q))
        {
            return true;
        }
        if (!string.IsNullOrEmpty(c.name) && c.name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        string pretty = C2_Tree.Class_DisplayName(c.GetType());
        if (pretty.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (c.GetType().Name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    static string Comp_Label(ImpComp c)
    {
        if (c == null)
        {
            return "";
        }
        if (!string.IsNullOrEmpty(c.name))
        {
            return c.name;
        }
        return C2_Tree.Class_DisplayName(c.GetType());
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

    void SelectItem(TTreeItem item, bool confirm)
    {
        if (item.is_disabled)
        {
            return;
        }
        if (IsNone(item.data))
        {
            none_selected = true;
            selected = null;
            Label_Set("None");
            if (confirm)
            {
                Confirm();
            }
            return;
        }
        if (item.data is ImpComp c && IsAccepted(c))
        {
            none_selected = false;
            selected = c;
            Label_Set(Comp_Label(c) + "  (" + C2_Tree.Class_DisplayName(c.GetType()) + ")");
            if (confirm)
            {
                Confirm();
            }
        }
    }

    void SelectCurrent()
    {
        if (current == null)
        {
            if (allow_none)
            {
                none_selected = true;
                selected = null;
                Label_Set("None");
                comp_tree.Tree_SelectData(NoneData);
            }
            return;
        }

        none_selected = false;
        selected = current;
        Label_Set(Comp_Label(current) + "  (" + C2_Tree.Class_DisplayName(current.GetType()) + ")");
        comp_tree.Tree_SelectData(current);
    }

    void Label_Set(string text)
    {
        selected_name.text = text ?? "";
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

    void Confirm()
    {
        if (!none_selected && selected == null)
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
