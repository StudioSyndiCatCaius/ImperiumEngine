using System.Numerics;
using Editor.Panel;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Dialog;

/*
 * This dialog opens when you select "Create Asset" from an ImpFile in the File Browser.
 * Layout:
 *  - left: list of all types & assets you can create. E.G (when importing a GLB)
 *      - A_Texture
 *          - texture_1
 *          - texture_2
 *      - A_Mesh
 *          - mesh_1
 *      - A_Animation
 *          - idle
 *          - walk
 *          - run
 */
public class DLG_CreateAssetFromFile : ImpDialog
{
    public C2_Box box = new();
    public C2_SearchBar search_bar = new();
    public C2_Tree offer_tree = new();
    public C2_Text selected_name = new();
    public C2_TextEdit txt_name = new();
    public C2_Button btn_all = new();
    public C2_Button btn_none = new();
    public C2_Button btn_confirm = new();
    public C2_Button btn_cancel = new();

    ImpFile _file;
    string _query = "";
    bool _ui;
    C2_Text _title_lbl;
    C2_Text _name_lbl;
    C2_Text _hint;
    TRow _selected;
    readonly List<TRow> _rows = new();

    class TRow
    {
        public TFileAssetOffer offer;
        public bool is_checked = true;
    }

    public static void Run(ImpFile file)
    {
        if (file == null)
        {
            return;
        }
        DLG_CreateAssetFromFile dlg = new();
        dlg._file = file;
        dlg.Show();
    }

    public DLG_CreateAssetFromFile()
    {
        box.style = new UI_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(480, 560),
            size_min = new Vector2(380, 420),
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

        offer_tree.layout = TLayout2.FULL;
        offer_tree.on_item_click = item =>
        {
            SelectItem(item, false);
        };
        offer_tree.on_item_double_click = item =>
        {
            SelectItem(item, true);
        };

        selected_name.style = UI_Text.MUTED;
        selected_name.layout = new TLayout2
        {
            size = new Vector2(0, 20),
            size_min = new Vector2(0, 20),
            orient_H = EUIViewportAlignment.Fill,
        };

        txt_name.text_placeholder = "Name";
        txt_name.on_text_changed = t =>
        {
            if (_selected != null && _selected.offer != null)
            {
                _selected.offer.name = t ?? "";
            }
        };
        txt_name.on_submit = _ => Confirm();

        btn_all.text = "All";
        btn_all.on_click = () => CheckAll(true);
        btn_none.text = "None";
        btn_none.on_click = () => CheckAll(false);

        btn_confirm.text = "Create";
        btn_confirm.on_click = Confirm;
        btn_cancel.text = "Cancel";
        btn_cancel.on_click = Cancel;
    }

    public override void Show()
    {
        _query = "";
        _selected = null;
        if (search_bar.text_edit != null)
        {
            search_bar.text_edit.text = "";
        }
        txt_name.text = "";
        txt_name.cursor = 0;
        LoadRows();
        EnsureUi();
        if (_title_lbl != null)
        {
            string file_name = "";
            if (_file != null && !string.IsNullOrEmpty(_file.filepath))
            {
                file_name = Path.GetFileName(_file.filepath);
            }
            if (string.IsNullOrEmpty(file_name))
            {
                _title_lbl.text = "Create Asset";
            }
            else
            {
                _title_lbl.text = "Create Asset from " + file_name;
            }
        }
        Hint_Set("");
        Label_Refresh();
        BuildTree();
        on_dismiss = Cancel;
        base.Show();
        if (Overlay != null)
        {
            Overlay.Child_Add(box);
        }
        if (_rows.Count == 0)
        {
            Hint_Set("Nothing to create from this file.");
        }
    }

    void LoadRows()
    {
        _rows.Clear();
        if (_file == null)
        {
            return;
        }
        List<TFileAssetOffer> offers = _file.Editor_ListCreateableAssets();
        for (int i = 0; i < offers.Count; i++)
        {
            TFileAssetOffer offer = offers[i];
            if (offer == null || offer.asset_type == null)
            {
                continue;
            }
            if (string.IsNullOrEmpty(offer.name))
            {
                offer.name = "Asset";
            }
            _rows.Add(new TRow
            {
                offer = offer,
                is_checked = true,
            });
        }
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
            text = "Create Asset",
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

        txt_name.layout = new TLayout2
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

        btn_all.layout = new TLayout2
        {
            size = new Vector2(70, 24),
            size_min = new Vector2(60, 24),
        };
        btn_none.layout = new TLayout2
        {
            size = new Vector2(70, 24),
            size_min = new Vector2(60, 24),
        };

        C2_List tools = new()
        {
            orentation = EUIOrentation.H,
            spacing = 8,
            layout = new TLayout2
            {
                size = new Vector2(0, 26),
                size_min = new Vector2(0, 26),
                orient_H = EUIViewportAlignment.Fill,
            },
        };
        tools.Child_Add(btn_all);
        tools.Child_Add(btn_none);

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
        col.Child_Add(tools);
        col.Child_Add(offer_tree);
        col.Child_Add(_name_lbl);
        col.Child_Add(txt_name);
        col.Child_Add(_hint);
        col.Child_Add(btns);
        box.Child_Add(col);
    }

    void Hint_Set(string text)
    {
        if (_hint != null)
        {
            _hint.text = text ?? "";
        }
    }

    int Checked_Count()
    {
        int n = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].is_checked)
            {
                n++;
            }
        }
        return n;
    }

    void Label_Refresh()
    {
        int n = Checked_Count();
        selected_name.text = n + " of " + _rows.Count + " selected";
        bool leaf = _selected != null;
        txt_name.is_visible = leaf;
        if (_name_lbl != null)
        {
            _name_lbl.is_visible = leaf;
        }
    }

    void CheckAll(bool on)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            _rows[i].is_checked = on;
        }
        Hint_Set("");
        Label_Refresh();
        BuildTree();
    }

    bool Row_Matches(TRow row)
    {
        if (string.IsNullOrEmpty(_query))
        {
            return true;
        }
        TFileAssetOffer offer = row.offer;
        if (offer == null)
        {
            return false;
        }
        if (offer.name != null && offer.name.Contains(_query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (offer.asset_type != null && offer.asset_type.Name.Contains(_query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    void BuildTree()
    {
        List<Type> types = new();
        for (int i = 0; i < _rows.Count; i++)
        {
            Type t = _rows[i].offer.asset_type;
            if (t == null)
            {
                continue;
            }
            bool have = false;
            for (int k = 0; k < types.Count; k++)
            {
                if (types[k] == t)
                {
                    have = true;
                    break;
                }
            }
            if (!have)
            {
                types.Add(t);
            }
        }

        List<TTreeItem> items = new();
        for (int t = 0; t < types.Count; t++)
        {
            Type type = types[t];
            List<TTreeItem> kids = new();
            for (int i = 0; i < _rows.Count; i++)
            {
                TRow row = _rows[i];
                if (row.offer.asset_type != type)
                {
                    continue;
                }
                if (!Row_Matches(row))
                {
                    continue;
                }
                kids.Add(Item_Row(row));
            }
            if (kids.Count == 0)
            {
                continue;
            }

            bool group_on = Group_AllChecked(type);
            A_Texture group_check = A_Texture.CHECKBOX_F;
            Color group_wash = Color.Blank;
            if (group_on)
            {
                group_check = A_Texture.CHECKBOX_T;
                group_wash = new Color(0, 96, 166, 40);
            }

            items.Add(new TTreeItem
            {
                sections = new[]
                {
                    new TTreeItemSection
                    {
                        icon = group_check,
                    },
                    new TTreeItemSection
                    {
                        text = type.Name,
                        icon = C2_Tree.Class_Icon(type),
                        icon_tint = ImpAsset.Color_ForType(type),
                    }
                },
                data = type,
                children = kids.ToArray(),
                row_tint = group_wash,
            });
        }

        offer_tree.Tree_SetItems(items);
        offer_tree.Tree_ExpandAll(true);
        if (_selected != null)
        {
            offer_tree.Tree_SelectData(_selected);
        }
    }

    TTreeItem Item_Row(TRow row)
    {
        TFileAssetOffer offer = row.offer;
        Texture2D? thumb = null;
        if (_file != null
            && offer.asset_type != null
            && typeof(A_Texture).IsAssignableFrom(offer.asset_type)
            && offer.source_index >= 0
            && offer.source_index < _file.src_textures.Count)
        {
            Texture2D tex = _file.src_textures[offer.source_index];
            if (tex.Id != 0)
            {
                thumb = tex;
            }
        }

        Color wash = Color.Blank;
        if (row.is_checked)
        {
            wash = new Color(0, 96, 166, 40);
        }

        return new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection
                {
                    icon = row.is_checked ? A_Texture.CHECKBOX_T : A_Texture.CHECKBOX_F,
                },
                new TTreeItemSection
                {
                    text = offer.name ?? "",
                    icon = C2_Tree.Class_Icon(offer.asset_type),
                    icon_texture = thumb,
                    icon_tint = ImpAsset.Color_ForType(offer.asset_type),
                },
            },
            data = row,
            row_tint = wash,
        };
    }

    void SelectItem(TTreeItem item, bool confirm)
    {
        if (item.data is Type type)
        {
            ToggleGroup(type);
            _selected = null;
            txt_name.text = "";
            Hint_Set("");
            Label_Refresh();
            BuildTree();
            offer_tree.Tree_SelectData(type);
            return;
        }
        if (item.data is not TRow row)
        {
            return;
        }

        if (confirm)
        {
            row.is_checked = true;
            _selected = row;
            NameField_Set(row);
            Confirm();
            return;
        }

        row.is_checked = !row.is_checked;
        _selected = row;
        NameField_Set(row);
        Hint_Set("");
        Label_Refresh();
        BuildTree();
        offer_tree.Tree_SelectData(row);
    }

    bool Group_AllChecked(Type type)
    {
        int n = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            TRow row = _rows[i];
            if (row.offer.asset_type != type)
            {
                continue;
            }
            if (!Row_Matches(row))
            {
                continue;
            }
            n++;
            if (!row.is_checked)
            {
                return false;
            }
        }
        return n > 0;
    }

    void ToggleGroup(Type type)
    {
        bool next = !Group_AllChecked(type);
        for (int i = 0; i < _rows.Count; i++)
        {
            TRow row = _rows[i];
            if (row.offer.asset_type != type)
            {
                continue;
            }
            if (!Row_Matches(row))
            {
                continue;
            }
            row.is_checked = next;
        }
    }

    void NameField_Set(TRow row)
    {
        string name = "";
        if (row.offer != null && row.offer.name != null)
        {
            name = row.offer.name;
        }
        txt_name.text = name;
        txt_name.cursor = name.Length;
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].input_hog = txt_name;
            txt_name.is_focused = true;
        }
    }

    void Confirm()
    {
        if (_file == null)
        {
            Hint_Set("No source file.");
            return;
        }

        List<TRow> pick = new();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].is_checked)
            {
                pick.Add(_rows[i]);
            }
        }
        if (pick.Count == 0)
        {
            Hint_Set("Select at least one asset.");
            return;
        }

        for (int i = 0; i < pick.Count; i++)
        {
            TFileAssetOffer offer = pick[i].offer;
            string name = ImpFile.AssetName_Sanitize(offer.name);
            if (string.IsNullOrEmpty(name))
            {
                Hint_Set("Name is required.");
                _selected = pick[i];
                NameField_Set(pick[i]);
                Label_Refresh();
                return;
            }
            offer.name = name;
        }

        int wrote = 0;
        ImpAsset last = null;
        for (int i = 0; i < pick.Count; i++)
        {
            TFileAssetOffer offer = pick[i].offer;
            ImpAsset asset = _file.Editor_WriteAsset(offer.asset_type, offer.name, offer.source_index);
            if (asset == null)
            {
                continue;
            }
            wrote++;
            last = asset;
        }

        if (wrote == 0)
        {
            Hint_Set("Could not write the files.");
            return;
        }

        PNL_FileBrowser.Browsers_Notify();
        if (wrote == 1 && last != null && ImpAsset.Editor_OnOpenAsset != null)
        {
            ImpAsset.Editor_OnOpenAsset.Invoke(last);
        }
        if (is_open)
        {
            Close();
        }
    }

    void Cancel()
    {
        if (is_open)
        {
            Close();
        }
    }
}
