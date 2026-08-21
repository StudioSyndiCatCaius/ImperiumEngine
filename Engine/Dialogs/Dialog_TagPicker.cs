using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Dialogs;

public class Dialog_TagPicker : ImpDialog
{
    public C2_Box box = new();
    public C2_SearchBar search_bar = new();
    public C2_Tree tag_tree = new();
    public C2_Text selected_name = new();
    public C2_TextEdit txt_new = new();
    public C2_Button btn_add = new();
    public C2_Button btn_confirm = new();
    public C2_Button btn_cancel = new();

    public bool is_multi_select;
    public bool allow_none = true;
    public TTag selected_tag_single;
    public TTagSet selected_tag_multi;
    public string title = "Select Tag";

    public Action<Dialog_TagPicker> on_confirm;
    public Action<Dialog_TagPicker> on_cancel;

    static readonly object NoneData = "";

    class TCreateTag
    {
        public TTag tag;
    }

    C2_Text _title_lbl;
    C2_Text _hint;
    bool _ui;
    string _query = "";

    class TTagNode
    {
        public string segment;
        public string full;
        public List<TTagNode> children = new();

        public TTagNode Child(string segment, string full)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (string.Equals(children[i].segment, segment, StringComparison.OrdinalIgnoreCase))
                {
                    return children[i];
                }
            }
            TTagNode made = new() { segment = segment, full = full };
            children.Add(made);
            return made;
        }
    }

    public static void Run(TTag current, Action<TTag> on_picked, Action on_cancel = null,
        string title = null, bool allow_none = true)
    {
        Dialog_TagPicker dlg = new();
        dlg.is_multi_select = false;
        dlg.selected_tag_single = current;
        dlg.allow_none = allow_none;
        if (!string.IsNullOrEmpty(title))
        {
            dlg.title = title;
        }
        else
        {
            dlg.title = "Select Tag";
        }
        dlg.on_confirm = d =>
        {
            on_picked?.Invoke(d.selected_tag_single);
        };
        dlg.on_cancel = _ => on_cancel?.Invoke();
        dlg.Show();
    }

    public static void Run(TTagSet current, Action<TTagSet> on_picked, Action on_cancel = null,
        string title = null)
    {
        Dialog_TagPicker dlg = new();
        dlg.is_multi_select = true;
        dlg.allow_none = false;
        if (current != null)
        {
            dlg.selected_tag_multi = current.Clone();
        }
        else
        {
            dlg.selected_tag_multi = new TTagSet();
        }
        if (!string.IsNullOrEmpty(title))
        {
            dlg.title = title;
        }
        else
        {
            dlg.title = "Select Tags";
        }
        dlg.on_confirm = d =>
        {
            TTagSet set = d.selected_tag_multi;
            if (set == null)
            {
                set = new TTagSet();
            }
            on_picked?.Invoke(set.Clone());
        };
        dlg.on_cancel = _ => on_cancel?.Invoke();
        dlg.Show();
    }

    public Dialog_TagPicker()
    {
        box.style = new UI_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(460, 560),
            size_min = new Vector2(360, 420),
        };

        search_bar.placeholder = "Search tags";
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

        tag_tree.layout = TLayout2.FULL;
        tag_tree.on_item_click = item =>
        {
            SelectItem(item, false);
        };
        tag_tree.on_item_double_click = item =>
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

        txt_new.text_placeholder = "New tag (e.g. Status.Burning)";
        txt_new.on_submit = _ => AddNew();

        btn_add.text = "Add";
        btn_add.on_click = AddNew;
        btn_confirm.text = "OK";
        btn_confirm.on_click = Confirm;
        btn_cancel.text = "Cancel";
        btn_cancel.on_click = Cancel;
    }

    public override void Show()
    {
        _query = "";
        if (search_bar.text_edit != null)
        {
            search_bar.text_edit.text = "";
        }
        txt_new.text = "";
        txt_new.cursor = 0;
        if (is_multi_select)
        {
            if (selected_tag_multi == null)
            {
                selected_tag_multi = new TTagSet();
            }
        }
        ImpTags.EnsureLoaded();
        EnsureUi();
        if (_title_lbl != null)
        {
            _title_lbl.text = title ?? "Select Tag";
        }
        Hint_Set("");
        BuildTree();
        SelectCurrent();
        Label_Refresh();
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
            text = title ?? "Select Tag",
            style = UI_Text.LIGHT,
            layout = new TLayout2
            {
                size = new Vector2(0, 26),
                size_min = new Vector2(0, 26),
                orient_H = EUIViewportAlignment.Fill,
            },
        };

        txt_new.layout = new TLayout2
        {
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
            orient_H = EUIViewportAlignment.Fill,
        };

        btn_add.layout = new TLayout2
        {
            size = new Vector2(70, 26),
            size_min = new Vector2(60, 26),
        };

        C2_List add_row = new()
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
        add_row.Child_Add(txt_new);
        add_row.Child_Add(btn_add);

        _hint = new C2_Text
        {
            text = "",
            style = UI_Text.MUTED,
            layout = new TLayout2
            {
                size = new Vector2(0, 16),
                size_min = new Vector2(0, 16),
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
        col.Child_Add(tag_tree);
        col.Child_Add(add_row);
        col.Child_Add(_hint);
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

        if (!is_multi_select && allow_none && (!searching || "none".Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(Item_None());
        }

        string create = ImpTags.Normalize(q);
        if (searching && ImpTags.IsValid(create) && !ImpTags.Contains(create))
        {
            items.Add(Item_Create(create));
        }

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        List<string> all = ImpTags.All();
        for (int i = 0; i < all.Count; i++)
        {
            names.Add(all[i]);
        }
        if (is_multi_select && selected_tag_multi != null)
        {
            foreach (TTag t in selected_tag_multi)
            {
                if (t.IsValid)
                {
                    names.Add(t.TagName);
                }
            }
        }
        else if (selected_tag_single.IsValid)
        {
            names.Add(selected_tag_single.TagName);
        }

        TTagNode root = new() { segment = "", full = "" };
        foreach (string name in names)
        {
            if (searching && !Tag_Matches(name, q))
            {
                continue;
            }
            string[] parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            TTagNode cur = root;
            string acc = "";
            for (int i = 0; i < parts.Length; i++)
            {
                if (acc.Length == 0)
                {
                    acc = parts[i];
                }
                else
                {
                    acc = acc + "." + parts[i];
                }
                cur = cur.Child(parts[i], acc);
            }
        }
        SortNode(root);

        if (searching)
        {
            FlattenMatches(root, items);
        }
        else
        {
            for (int i = 0; i < root.children.Count; i++)
            {
                items.Add(Item_Node(root.children[i]));
            }
        }

        object keep = tag_tree.selected_data;
        List<string> expanded = tag_tree.Tree_ExpandedKeys();
        tag_tree.Tree_SetItems(items);
        if (searching)
        {
            tag_tree.Tree_ExpandAll(true);
        }
        else
        {
            tag_tree.Tree_SetExpandedKeys(expanded);
            ExpandSelection();
        }
        if (keep != null)
        {
            tag_tree.Tree_SelectData(keep);
        }
    }

    void FlattenMatches(TTagNode node, List<TTreeItem> items)
    {
        if (!string.IsNullOrEmpty(node.full) && Tag_Matches(node.full, _query))
        {
            items.Add(Item_Tag(node.full, node.full, false));
        }
        for (int i = 0; i < node.children.Count; i++)
        {
            FlattenMatches(node.children[i], items);
        }
    }

    static void SortNode(TTagNode node)
    {
        node.children.Sort((a, b) => string.Compare(a.segment, b.segment, StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < node.children.Count; i++)
        {
            SortNode(node.children[i]);
        }
    }

    static bool Tag_Matches(string name, string q)
    {
        if (string.IsNullOrEmpty(q))
        {
            return true;
        }
        if (name != null && name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    TTreeItem Item_Node(TTagNode node)
    {
        List<TTreeItem> kids = new();
        for (int i = 0; i < node.children.Count; i++)
        {
            kids.Add(Item_Node(node.children[i]));
        }
        TTreeItem item = Item_Tag(node.full, node.segment, false);
        item.children = kids.ToArray();
        return item;
    }

    TTreeItem Item_Tag(string full, string label, bool is_create)
    {
        TTag tag = new TTag(full);
        bool on = is_multi_select && selected_tag_multi != null && selected_tag_multi.HasTagExact(tag);
        List<TTreeItemSection> sections = new();
        if (is_multi_select)
        {
            sections.Add(new TTreeItemSection
            {
                icon = on ? A_Texture.CHECKBOX_T : A_Texture.CHECKBOX_F,
            });
        }
        sections.Add(new TTreeItemSection
        {
            text = label,
        });
        if (is_create)
        {
            sections.Add(new TTreeItemSection
            {
                text = "Create",
                color = new Color(130, 200, 130, 255),
            });
        }
        else if (!string.IsNullOrEmpty(full) && !string.Equals(full, label, StringComparison.Ordinal))
        {
            sections.Add(new TTreeItemSection
            {
                text = full,
                color = new Color(130, 130, 130, 255),
            });
        }
        Color wash = Color.Blank;
        if (on)
        {
            wash = new Color(0, 96, 166, 40);
        }
        return new TTreeItem
        {
            sections = sections.ToArray(),
            data = tag,
            row_tint = wash,
        };
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

    TTreeItem Item_Create(string name)
    {
        TTreeItem item = Item_Tag(name, name, true);
        item.data = new TCreateTag { tag = new TTag(name) };
        return item;
    }

    void SelectItem(TTreeItem item, bool confirm)
    {
        if (item.is_disabled)
        {
            return;
        }
        if (IsNone(item.data))
        {
            selected_tag_single = TTag.None;
            Label_Refresh();
            if (confirm)
            {
                Confirm();
            }
            return;
        }

        TTag tag = TTag.None;
        bool created = false;
        if (item.data is TCreateTag create)
        {
            tag = create.tag;
            created = true;
        }
        else if (item.data is TTag t)
        {
            tag = t;
        }
        if (!tag.IsValid)
        {
            return;
        }

        if (created || !ImpTags.Contains(tag))
        {
            ImpTags.Register(tag, true);
        }

        if (is_multi_select)
        {
            if (selected_tag_multi == null)
            {
                selected_tag_multi = new TTagSet();
            }
            if (selected_tag_multi.HasTagExact(tag))
            {
                selected_tag_multi.RemoveTag(tag);
            }
            else
            {
                selected_tag_multi.AddTag(tag);
            }
            Label_Refresh();
            BuildTree();
            tag_tree.Tree_SelectData(tag);
            return;
        }

        selected_tag_single = tag;
        Label_Refresh();
        if (confirm)
        {
            Confirm();
        }
    }

    void SelectCurrent()
    {
        if (is_multi_select)
        {
            return;
        }
        if (!selected_tag_single.IsValid)
        {
            if (allow_none)
            {
                tag_tree.Tree_SelectData(NoneData);
            }
            return;
        }
        tag_tree.Tree_SelectData(selected_tag_single);
        ExpandTo(selected_tag_single.TagName);
    }

    void ExpandSelection()
    {
        if (is_multi_select)
        {
            if (selected_tag_multi == null)
            {
                return;
            }
            foreach (TTag t in selected_tag_multi)
            {
                ExpandTo(t.TagName);
            }
            return;
        }
        if (selected_tag_single.IsValid)
        {
            ExpandTo(selected_tag_single.TagName);
        }
    }

    void ExpandTo(string full)
    {
        if (string.IsNullOrEmpty(full))
        {
            return;
        }
        string[] parts = full.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string acc = "";
        for (int i = 0; i < parts.Length; i++)
        {
            if (acc.Length == 0)
            {
                acc = parts[i];
            }
            else
            {
                acc = acc + "." + parts[i];
            }
            tag_tree.Tree_ExpandKey(acc, true);
        }
    }

    void AddNew()
    {
        string name = ImpTags.Normalize(txt_new.text);
        if (!ImpTags.IsValid(name))
        {
            Hint_Set("Use dots, e.g. Status.Burning");
            txt_new.is_focused = true;
            return;
        }
        ImpTags.Register(name, true);
        TTag tag = new TTag(name);
        txt_new.text = "";
        txt_new.cursor = 0;
        Hint_Set("");
        if (is_multi_select)
        {
            if (selected_tag_multi == null)
            {
                selected_tag_multi = new TTagSet();
            }
            selected_tag_multi.AddTag(tag);
        }
        else
        {
            selected_tag_single = tag;
        }
        _query = "";
        if (search_bar.text_edit != null)
        {
            search_bar.text_edit.text = "";
        }
        BuildTree();
        tag_tree.Tree_SelectData(tag);
        ExpandTo(tag.TagName);
        Label_Refresh();
    }

    void Label_Refresh()
    {
        if (is_multi_select)
        {
            TTagSet set = selected_tag_multi;
            if (set == null || set.IsEmpty)
            {
                selected_name.text = "No tags selected";
                return;
            }
            if (set.Count == 1)
            {
                selected_name.text = set.ToString();
                return;
            }
            selected_name.text = set.Count + " tags  —  " + set.ToString();
            return;
        }
        if (selected_tag_single.IsValid)
        {
            selected_name.text = selected_tag_single.TagName;
            return;
        }
        selected_name.text = "None";
    }

    void Hint_Set(string text)
    {
        if (_hint != null)
        {
            _hint.text = text ?? "";
        }
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
        if (!is_multi_select)
        {
            if (!selected_tag_single.IsValid && !allow_none)
            {
                return;
            }
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
