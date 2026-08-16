using System.Numerics;
using System.Reflection;
using Editor.Dialog;
using ImperiumEngine;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace Editor.Panel;

public class PNL_SceneTree : EdPanel
{
    C2_SearchBar search_bar = new();
    C2_Tree comp_tree = new();

    public ImpScene scene;
    public ImpComp? root_comp;

    public Action<ImpComp> on_comp_click;
    public Action<TTreeItem, TTreeItem, ETreeDrop> on_item_drop;

    string _hier_sig = "";

    public PNL_SceneTree()
    {
        name = "Outliner";
        layout = TLayout2.FULL;

        search_bar.placeholder = "Search";
        search_bar.layout = new TLayout2
        {
            orient_H = EUIViewportAlignment.Fill,
            size = new Vector2(0, 26),
            size_min = new Vector2(0, 26),
        };
        search_bar.on_search = _ => Refresh(false);

        comp_tree.allow_reorder = true;
        comp_tree.layout = TLayout2.FULL;
        comp_tree.on_item_drop = (src, dst, where) => on_item_drop?.Invoke(src, dst, where);
        comp_tree.on_item_drop_external = OnExternalDrop;
        comp_tree.on_item_click = item =>
        {
            if (item.data is ImpComp c) on_comp_click?.Invoke(c);
        };
        comp_tree.on_item_right_click = OnCompRightClick;
        comp_tree.on_background_right_click = OnEmptyRightClick;

        C2_List col = new()
        {
            orentation = EUIOrentation.V,
            is_scrollable = false,
            layout = TLayout2.FULL,
            spacing = 0,
        };
        col.Child_Add(search_bar);
        col.Child_Add(comp_tree);
        Child_Add(col);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        ImpComp root = Root();
        string sig = root != null ? HierSig(root) : "";
        if (sig != _hier_sig) Refresh(false);
    }

    ImpComp Root() => root_comp ?? scene?.root;

    public void Refresh(bool expand_all)
    {
        ImpComp root = Root();
        _hier_sig = root != null ? HierSig(root) : "";
        object keep = comp_tree.selected_data;
        if (root == null)
        {
            comp_tree.Tree_Clear();
            return;
        }
        comp_tree.Tree_Populate_FromComp(root);
        if (expand_all) comp_tree.Tree_ExpandAll(true);
        if (keep != null) comp_tree.Tree_SelectData(keep);
    }

    public void Select(ImpComp? comp)
    {
        comp_tree.Tree_SelectData(comp);
    }

    public void Expand(ImpComp comp)
    {
        if (comp == null) return;
        comp_tree.Tree_ExpandKey(C2_Tree.KeyOf(comp), true);
    }

    static string HierSig(ImpComp root)
    {
        System.Text.StringBuilder sb = new();
        void Walk(ImpComp c)
        {
            if (c == null) return;
            sb.Append(c.GetHashCode()).Append(':').Append(c.name ?? "").Append(':').Append(c.children.Count).Append(';');
            for (int i = 0; i < c.children.Count; i++) Walk(c.children[i]);
        }
        Walk(root);
        return sb.ToString();
    }

    void OnCompRightClick(TTreeItem item)
    {
        if (item.data is not ImpComp c) return;
        on_comp_click?.Invoke(c);
        Vector2 pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
        bool foreign = c.IsPackedForeign;
        bool inst = c.IsInstanceRoot;
        bool is_root = c == Root();
        ImpPlayer.Popup_Run(this, new A_PopupConfig
        {
            options = new List<TPopupMenuOption>
            {
                new() { text = "Duplicate", is_disabled = foreign || is_root || c.parent == null, on_press = () => Duplicate(c) },
                new() { text = "Delete", is_disabled = foreign || is_root, on_press = () => Delete(c) },
                new() { text = "Change Type", is_disabled = foreign || inst, on_press = () => ChooseType("Change Type", t => ChangeType(c, t)) },
                new() { text = "Add Child", is_disabled = foreign || inst, on_press = () => ChooseType("Add Child", t => AddChild(c, t)) },
            },
        }, null, pos);
    }

    void OnEmptyRightClick()
    {
        ImpComp root = Root();
        if (root == null) return;
        Vector2 pos = ImpPlayer.players.Count > 0 ? ImpPlayer.players[0].cursor.position : Vector2.Zero;
        ImpPlayer.Popup_Run(this, new A_PopupConfig
        {
            options = new List<TPopupMenuOption>
            {
                new() { text = "Add Comp", is_disabled = root.IsPackedForeign || root.IsInstanceRoot, on_press = () => ChooseType("Add Comp", t => AddChild(root, t)) },
            },
        }, null, pos);
    }

    static void ChooseType(string title, Action<Type> picked)
    {
        DLG_ChooseComp.Run(picked, title);
    }

    void Duplicate(ImpComp s)
    {
        if (s == null || s.parent == null || s == Root() || s.IsPackedForeign) return;
        ImpComp copy = s.Clone();
        if (copy == null) return;
        copy.name = ImpComp.Name_Unique(s.parent, string.IsNullOrEmpty(s.name) ? copy.GetType().Name : s.name);
        int idx = s.parent.children.IndexOf(s);
        s.parent.Child_Insert(idx + 1, copy);
        if (copy is Imp3D c3) c3.Position_Set(c3.Position_Get(false) + new Vector3(0.5f, 0f, 0f), false);
        if (copy is Imp2D c2) c2.Position_Set(c2.Position_Get(false) + new Vector2(16f, 16f), false);
        ImpUndo.Comp_Moved(copy, default, "Duplicate");
        on_comp_click?.Invoke(copy);
    }

    void Delete(ImpComp s)
    {
        if (s == null || s == Root() || s.IsPackedForeign) return;
        ImpComp parent = s.parent;
        TCompPlace from = ImpUndo.Place_Get(s);
        s.Detach();
        ImpUndo.Comp_Moved(s, from, "Delete");
        if (parent != null) on_comp_click?.Invoke(parent);
    }

    void OnExternalDrop(object payload, TTreeItem item)
    {
        if (payload is Type t && item.data is ImpComp parent)
        {
            AddChild(parent, t);
        }
    }

    public void AddComp(Type type, ImpComp parent = null)
    {
        if (parent == null)
        {
            parent = Root();
        }
        AddChild(parent, type);
    }

    void AddChild(ImpComp parent, Type type)
    {
        if (parent == null || type == null) return;
        if (parent.IsPackedForeign || parent.IsInstanceRoot) return;
        if (!typeof(ImpComp).IsAssignableFrom(type) || type.IsAbstract) return;
        if (Activator.CreateInstance(type) is not ImpComp n) return;
        n.name = ImpComp.Name_Unique(parent, type.Name);
        parent.Child_Add(n);
        ImpUndo.Comp_Moved(n, default, "Add Comp");
        Expand(parent);
        on_comp_click?.Invoke(n);
    }

    void ChangeType(ImpComp src, Type type)
    {
        if (src == null || type == null || type == src.GetType()) return;
        if (src.IsInstanceRoot || src.IsPackedForeign) return;
        if (!typeof(ImpComp).IsAssignableFrom(type) || type.IsAbstract) return;
        if (Activator.CreateInstance(type) is not ImpComp dst) return;

        CopyCompat(src, dst);
        dst.name = src.name;

        ImpScene sc = src.scene;
        bool is_root = sc != null && sc.root == src;
        ImpComp parent = src.parent;
        int idx = parent?.children.IndexOf(src) ?? -1;

        ImpUndo.Push("Change Type",
            () => SwapIn(dst, src, sc, is_root, parent, idx),
            () => SwapIn(src, dst, sc, is_root, parent, idx));
        SwapIn(src, dst, sc, is_root, parent, idx);
        on_comp_click?.Invoke(dst);
    }

    static void SwapIn(ImpComp outgoing, ImpComp incoming, ImpScene sc, bool is_root, ImpComp parent, int idx)
    {
        List<ImpComp> kids = new(outgoing.children);
        for (int i = 0; i < kids.Count; i++) kids[i].Detach();
        if (is_root && sc != null)
            sc.root = incoming;
        else
        {
            outgoing.Detach();
            if (parent == null) return;
            if (idx >= 0) parent.Child_Insert(idx, incoming);
            else parent.Child_Add(incoming);
        }
        for (int i = 0; i < kids.Count; i++) incoming.Child_Add(kids[i]);
    }

    static void CopyCompat(ImpComp src, ImpComp dst)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Dictionary<string, FieldInfo> map = new();
        foreach (FieldInfo f in dst.GetType().GetFields(flags))
        {
            if (f.IsInitOnly || f.IsLiteral) continue;
            map[f.Name] = f;
        }
        foreach (FieldInfo f in src.GetType().GetFields(flags))
        {
            if (f.IsInitOnly || f.IsLiteral) continue;
            if (f.Name is "parent" or "children" or "input_owner" or "is_destroying" or "_scene"
                or "packed" or "packed_from" or "option_button") continue;
            if (f.Name.StartsWith("_e_") || f.Name.StartsWith("_c_")) continue;
            if (!map.TryGetValue(f.Name, out FieldInfo df) || df.FieldType != f.FieldType) continue;
            df.SetValue(dst, f.GetValue(src));
        }
    }
}
