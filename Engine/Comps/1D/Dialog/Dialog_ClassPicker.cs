using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._1D.Dialog;

public class Dialog_ClassPicker : C1_Dialog
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

    public string title = "Choose Class";

    public Action<Dialog_ClassPicker> on_confirm;
    public Action<Dialog_ClassPicker> on_cancel;

    C2_Text _title_lbl;
    bool _ui;

    public Dialog_ClassPicker()
    {
        box.style = new UiStyle_Box { tint = new Color(42, 42, 42, 255) };
        box.cursor_filter = ECursorFilter.Hit;
        box.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Center,
            orient_V = EUIViewportAlignment.Center,
            size = new Vector2(420, 500),
            size_min = new Vector2(360, 360),
        };

        search_bar.placeholder = "Search";
        search_bar.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            orient_V = EUIViewportAlignment.Start,
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
        };
        search_bar.on_search = q => class_tree.Tree_FilterClasses(q);

        class_tree.layout = TLayout2.FULL;
        class_tree.on_item_click = item =>
        {
            if (item.is_disabled || item.data is not Type t || t.IsAbstract) return;
            selected_type = t;
            class_name.text = C2_Tree.Class_DisplayName(t);
        };
        class_tree.on_item_double_click = item =>
        {
            if (item.is_disabled || item.data is not Type t || t.IsAbstract) return;
            selected_type = t;
            Confirm();
        };

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
        class_name.text = "";
        if (search_bar.text_edit != null) search_bar.text_edit.text = "";
        BuildTree();
        EnsureUi();
        if (_title_lbl != null) _title_lbl.text = title ?? "Choose Class";
        txtedit_create_name.is_visible = is_create_new;
        base.Show();
        if (Shade != null) Shade.on_click = Cancel;
        if (Overlay is C2_DialogHost host) host.on_escape = Cancel;
        Overlay?.Child_Add(box);
    }

    public override void Close()
    {
        Hog_Release();
        box?.Detach();
        base.Close();
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

        txtedit_create_name.layout = new TLayout2
        {
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
            orient_H = EUIViewportAlignment.Fill,
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
        col.Child_Add(txtedit_create_name);
        col.Child_Add(btns);
        box.Child_Add(col);
    }

    void Confirm()
    {
        if (selected_type == null || selected_type.IsAbstract) return;
        on_confirm?.Invoke(this);
        if (is_open) Close();
    }

    void Cancel()
    {
        on_cancel?.Invoke(this);
        if (is_open) Close();
    }
}
