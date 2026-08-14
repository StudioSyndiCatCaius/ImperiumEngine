using System.Numerics;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public struct TTreeItem
{
    public TTreeItemSection[] sections;
    public TTreeItem[] children;
    public object data;
}

public struct TTreeItemSection
{
    public string text;
    public A_Texture icon;
    public Color color;
}

public enum ETreeDrop { Before, After, Child }

public class C2_Tree : ImpComp2D
{
    public UiStyle_Box style_box = UiStyle_Box.STYLE_BKG_DARK;
    public UiStyle_Text text_style = UiStyle_Text.LIGHT;
    public UiStyle_Text text_style_selected = UiStyle_Text.LIGHT;
    public Color color_row = new(40, 40, 40, 0);
    public Color color_row_hover = new(70, 70, 74, 255);
    public Color color_row_selected = new(0, 96, 166, 255);
    public float row_height = 22f;
    public float indent_size = 16f;
    public float icon_size = 14f;

    public Action<TTreeItem> on_item_click;
    public Action<TTreeItem> on_item_right_click;
    public Action<TTreeItem> on_item_double_click;
    public Action<TTreeItem, TTreeItem, ETreeDrop> on_item_drop;
    //Something from outside the tree was dropped onto a row (a file browser thumbnail, say).
    public Action<object, TTreeItem> on_item_drop_external;
    //What a row hands over when dragged elsewhere. Null (or a null result) means rows can't be dragged out.
    public Func<TTreeItem, object> item_drag_payload;
    public bool allow_reorder;

    public TTreeItem selected_item;
    public object selected_data;

    readonly List<TreeNode> _roots = new();
    readonly HashSet<string> _expanded = new(StringComparer.OrdinalIgnoreCase);
    TreeNode _selected_node;
    internal TreeNode drop_node;
    internal ETreeDrop drop_kind;

    C2_ScrollBox _scroll;

    public C2_Tree()
    {
        cursor_filter = ECursorFilter.Pass;
        view_alighnment_H = EUIViewportAlignment.Fill;
        view_alighnment_V = EUIViewportAlignment.Fill;
        EnsureScroll();
    }

    void EnsureScroll()
    {
        if (_scroll != null) return;
        _scroll = new C2_ScrollBox
        {
            view_alighnment_H = EUIViewportAlignment.Fill,
            view_alighnment_V = EUIViewportAlignment.Fill,
            alignment = EUIAlignment.Vertical,
            style = style_box,
        };
        Child_Add(_scroll);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        EnsureScroll();
        if (!children.Contains(_scroll)) Child_Add(_scroll);
        _scroll.style = style_box;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (style_box != null) style_box.Draw(Dimensions_Get());
    }

    public void Tree_Clear()
    {
        _roots.Clear();
        _selected_node = null;
        selected_data = null;
        selected_item = default;
        RebuildRows();
    }

    public void Tree_Add(TTreeItem item)
    {
        _roots.Add(ToNode(item, null));
        RebuildRows();
    }

    public void Tree_SetItems(IEnumerable<TTreeItem> items)
    {
        _roots.Clear();
        if (items != null)
        {
            foreach (TTreeItem item in items)
                _roots.Add(ToNode(item, null));
        }
        RebuildRows();
    }

    public void Tree_ExpandAll(bool expanded = true)
    {
        void Walk(TreeNode n)
        {
            if (expanded) _expanded.Add(n.key);
            else _expanded.Remove(n.key);
            n.expanded = expanded;
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        for (int i = 0; i < _roots.Count; i++) Walk(_roots[i]);
        RebuildRows();
    }

    public void Tree_ExpandKey(string key, bool expanded = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (expanded) _expanded.Add(key);
        else _expanded.Remove(key);
        RebuildRows();
    }

    public void Tree_SelectData(object data)
    {
        if (data == null)
        {
            _selected_node = null;
            selected_data = null;
            selected_item = default;
            RebuildRows();
            return;
        }
        TreeNode found = null;
        void Walk(TreeNode n)
        {
            if (found != null) return;
            if (Equals(n.item.data, data) || (n.item.data is TDirectory a && data is TDirectory b && PathsEqual(a.path, b.path)))
                found = n;
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        for (int i = 0; i < _roots.Count; i++) Walk(_roots[i]);
        if (found != null) SelectNode(found, false);
        RebuildRows();
    }

    public void Tree_Populate_FromComp(ImpComp comp)
    {
        Tree_Clear();
        if (comp == null) return;
        Tree_Add(FromComp(comp));
        Tree_ExpandKey(KeyOf(comp), true);
    }

    public void Tree_Populate_FromClasses(Type type)
    {
        Tree_Clear();
        if (type == null) return;

        List<Type> types = new();
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] found;
            try { found = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { found = ex.Types.Where(t => t != null).ToArray()!; }
            foreach (Type t in found)
            {
                if (t == null || t.IsAbstract) continue;
                if (!type.IsAssignableFrom(t)) continue;
                types.Add(t);
            }
        }
        if (!type.IsAbstract && !types.Contains(type)) types.Add(type);

        Dictionary<Type, TTreeItem> map = new();
        foreach (Type t in types)
        {
            map[t] = new TTreeItem
            {
                sections = new[] { new TTreeItemSection { text = t.Name } },
                data = t,
                children = Array.Empty<TTreeItem>(),
            };
        }

        List<TTreeItem> roots = new();
        foreach (Type t in types)
        {
            Type parent = t.BaseType;
            while (parent != null && !map.ContainsKey(parent))
                parent = parent.BaseType;
            if (parent != null && map.ContainsKey(parent) && parent != t)
            {
                TTreeItem p = map[parent];
                List<TTreeItem> kids = p.children != null ? p.children.ToList() : new List<TTreeItem>();
                kids.Add(map[t]);
                p.children = kids.ToArray();
                map[parent] = p;
            }
            else roots.Add(map[t]);
        }

        Tree_SetItems(roots);
        if (roots.Count > 0) Tree_ExpandKey(KeyOf(roots[0].data), true);
    }

    TTreeItem FromComp(ImpComp comp)
    {
        TTreeItem[] kids = Array.Empty<TTreeItem>();
        if (comp.children.Count > 0)
        {
            kids = new TTreeItem[comp.children.Count];
            for (int i = 0; i < comp.children.Count; i++)
                kids[i] = FromComp(comp.children[i]);
        }
        A_Texture icon = A_Texture.ICO_COMP;
        if (comp is ImpComp3D) icon = A_Texture.ICO_COMP3D;
        else if (comp is ImpComp2D) icon = A_Texture.ICO_COMP2D;
        Color name_col = default;
        if (comp.IsInstanceRoot || comp.IsPackedForeign)
            name_col = new Color(236, 196, 82, 255);
        if (comp.IsInstanceRoot)
            _expanded.Add(KeyOf(comp));
        return new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection
                {
                    text = string.IsNullOrEmpty(comp.name) ? comp.GetType().Name : comp.name,
                    icon = icon,
                    color = name_col,
                }
            },
            children = kids,
            data = comp,
        };
    }

    TreeNode ToNode(TTreeItem item, TreeNode parent)
    {
        TreeNode n = new()
        {
            item = item,
            parent = parent,
            key = KeyOf(item.data, item),
        };
        n.expanded = _expanded.Contains(n.key);
        if (item.children != null)
        {
            for (int i = 0; i < item.children.Length; i++)
                n.children.Add(ToNode(item.children[i], n));
        }
        return n;
    }

    public void RebuildRows()
    {
        EnsureScroll();
        _scroll.Child_RemoveAll();

        List<TreeNode> flat = new();
        void Walk(TreeNode n)
        {
            n.expanded = _expanded.Contains(n.key);
            flat.Add(n);
            if (!n.expanded) return;
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        for (int i = 0; i < _roots.Count; i++) Walk(_roots[i]);

        for (int i = 0; i < flat.Count; i++)
        {
            TreeNode n = flat[i];
            int depth = 0;
            TreeNode p = n.parent;
            while (p != null) { depth++; p = p.parent; }
            C2_TreeRow row = new(this, n, depth)
            {
                size = new Vector2(0, row_height),
                size_min = new Vector2(0, row_height),
                view_alighnment_H = EUIViewportAlignment.Fill,
            };
            _scroll.Child_Add(row);
        }
    }

    internal void ToggleExpand(TreeNode node)
    {
        if (node.children.Count == 0) return;
        if (_expanded.Contains(node.key)) _expanded.Remove(node.key);
        else _expanded.Add(node.key);
        node.expanded = _expanded.Contains(node.key);
        RebuildRows();
    }

    internal void SelectNode(TreeNode node, bool fire)
    {
        _selected_node = node;
        selected_item = node.item;
        selected_data = node.item.data;
        if (fire) on_item_click?.Invoke(node.item);
    }

    internal void OpenNode(TreeNode node)
    {
        on_item_double_click?.Invoke(node.item);
    }

    internal void RightClickNode(TreeNode node)
    {
        _selected_node = node;
        selected_item = node.item;
        selected_data = node.item.data;
        on_item_right_click?.Invoke(node.item);
        RebuildRows();
    }

    internal bool IsSelected(TreeNode node) => _selected_node == node || (node != null && _selected_node != null && node.key == _selected_node.key);

    internal void SetDrop(TreeNode n, ETreeDrop k)
    {
        drop_node = n;
        drop_kind = k;
    }

    internal void ClearDrop()
    {
        drop_node = null;
    }

    internal void CommitDropExternal(object payload, TreeNode dst)
    {
        ClearDrop();
        if (payload == null || dst == null) return;
        on_item_drop_external?.Invoke(payload, dst.item);
    }

    internal void CommitDrop(TreeNode src, TreeNode dst, ETreeDrop kind)
    {
        ClearDrop();
        if (src == null || dst == null || src == dst) return;
        for (TreeNode p = dst; p != null; p = p.parent)
            if (p == src) return;
        on_item_drop?.Invoke(src.item, dst.item, kind);
    }

    public static string KeyOf(object data, TTreeItem item = default)
    {
        if (data is TDirectory dir && !string.IsNullOrEmpty(dir.path)) return dir.path;
        if (data is TFile file && !string.IsNullOrEmpty(file.path)) return file.path;
        if (data is Type t) return t.FullName ?? t.Name;
        if (data is ImpComp c) return "comp:" + c.GetHashCode();
        if (data is ImpAsset a && !string.IsNullOrEmpty(a.filepath)) return a.filepath;
        if (item.sections is { Length: > 0 } && !string.IsNullOrEmpty(item.sections[0].text))
            return item.sections[0].text;
        return data?.ToString() ?? "";
    }

    static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    internal class TreeNode
    {
        public TTreeItem item;
        public bool expanded;
        public string key;
        public TreeNode parent;
        public List<TreeNode> children = new();
    }
}

class C2_TreeRow : ImpComp2D
{
    readonly C2_Tree _tree;
    readonly C2_Tree.TreeNode _node;
    readonly int _depth;
    bool _hover;
    double _last_click;

    public C2_TreeRow(C2_Tree tree, C2_Tree.TreeNode node, int depth)
    {
        _tree = tree;
        _node = node;
        _depth = depth;
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
    }

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        Color bg = _tree.IsSelected(_node)
            ? _tree.color_row_selected
            : (_hover ? _tree.color_row_hover : _tree.color_row);
        if (bg.A > 0) Raylib.DrawRectangleV(dim.position, dim.size, bg);

        if (_tree.drop_node == _node)
        {
            Color acc = new(0, 170, 255, 255);
            if (_tree.drop_kind == ETreeDrop.Child)
                Raylib.DrawRectangleLinesEx(new Rectangle(dim.position.X + 1, dim.position.Y + 1, dim.size.X - 2, dim.size.Y - 2), 2f, acc);
            else
            {
                float y = _tree.drop_kind == ETreeDrop.Before ? dim.position.Y : dim.position.Y + dim.size.Y - 2;
                Raylib.DrawRectangleV(new Vector2(dim.position.X + 4, y), new Vector2(dim.size.X - 8, 2), acc);
            }
        }

        float x = dim.position.X + 4 + _depth * _tree.indent_size;
        float mid_y = dim.position.Y + dim.size.Y * 0.5f;

        bool has_kids = _node.children.Count > 0;
        A_Texture arrow = has_kids
            ? (_node.expanded ? A_Texture.ICO_ARROW_D : A_Texture.ICO_ARROW_R)
            : null;
        bool drew_arrow = false;
        if (arrow != null)
        {
            Texture2D tex = arrow.texture;
            if (tex.Id != 0)
            {
                float s = 10;
                Raylib.DrawTexturePro(tex,
                    new Rectangle(0, 0, tex.Width, tex.Height),
                    new Rectangle(x, mid_y - s * 0.5f, s, s),
                    Vector2.Zero, 0f, Color.White);
                drew_arrow = true;
            }
        }
        if (has_kids && !drew_arrow)
        {
            Color ac = new(200, 200, 200, 255);
            if (_node.expanded)
                Raylib.DrawTriangle(
                    new Vector2(x + 1, mid_y - 3),
                    new Vector2(x + 9, mid_y - 3),
                    new Vector2(x + 5, mid_y + 4), ac);
            else
                Raylib.DrawTriangle(
                    new Vector2(x + 2, mid_y - 5),
                    new Vector2(x + 2, mid_y + 5),
                    new Vector2(x + 9, mid_y), ac);
        }
        x += 14;

        TTreeItemSection[] sections = _node.item.sections;
        if (sections != null)
        {
            for (int i = 0; i < sections.Length; i++)
            {
                TTreeItemSection s = sections[i];
                if (s.icon != null)
                {
                    Texture2D tex = s.icon.texture;
                    if (tex.Id != 0)
                    {
                        float sz = _tree.icon_size;
                        Raylib.DrawTexturePro(tex,
                            new Rectangle(0, 0, tex.Width, tex.Height),
                            new Rectangle(x, mid_y - sz * 0.5f, sz, sz),
                            Vector2.Zero, 0f, Color.White);
                        x += sz + 4;
                    }
                }
                if (!string.IsNullOrEmpty(s.text))
                {
                    UiStyle_Text style = _tree.IsSelected(_node) ? _tree.text_style_selected : _tree.text_style;
                    Color prev = default;
                    bool tint = s.color.A > 0 && style != null;
                    if (tint) { prev = style.color; style.color = s.color; }
                    float remain = dim.position.X + dim.size.X - x - 4;
                    if (remain > 0)
                        style?.Draw(s.text, new Vector2(x, dim.position.Y), new Vector2(remain, dim.size.Y),
                            0, ETextWrap.None, EUIPositionAlignment.Center, EUIPositionAlignment.Start);
                    if (tint) style.color = prev;
                    Vector2 m = Raylib.MeasureTextEx(
                        style?.font != null ? style.font.font : Raylib.GetFontDefault(),
                        s.text, style != null && style.size > 0 ? style.size : 13, 1f);
                    x += m.X + 8;
                }
            }
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
            _tree.RightClickNode(_node);
            return;
        }
        if (evnt != ECursorEvent.Select_A) return;

        TDimensions2 dim = Dimensions_Get();
        float arrow_x = dim.position.X + 4 + _depth * _tree.indent_size;
        if (_node.children.Count > 0
            && player.cursor.position.X >= arrow_x - 2
            && player.cursor.position.X <= arrow_x + 16)
        {
            _tree.ToggleExpand(_node);
            return;
        }

        double now = Raylib.GetTime();
        bool dbl = now - _last_click < 0.35;
        _last_click = now;
        _tree.SelectNode(_node, true);
        if (dbl) _tree.OpenNode(_node);
    }

    public override bool CursorGrab_IsEnabled(ImpPlayer player)
    {
        if (CursorGrab_Payload() != null) return true;
        return _tree.allow_reorder && _tree.on_item_drop != null
            && _node.item.data is ImpComp c && !c.IsPackedForeign;
    }

    public override object CursorGrab_Payload()
    {
        return _tree.item_drag_payload?.Invoke(_node.item);
    }

    public override void CursorGrab_Begin(ImpPlayer player)
    {
        base.CursorGrab_Begin(player);
        _tree.SelectNode(_node, false);
    }

    public override void CursorGrab_Update(ImpPlayer player, double dt)
    {
        base.CursorGrab_Update(player, dt);
        if (player.cursor_target is C2_TreeRow row && row._tree == _tree)
            row.ApplyDropHover(player);
        else _tree.ClearDrop();
    }

    public override void CursorGrab_Drop(ImpPlayer player, ImpComp target)
    {
        base.CursorGrab_Drop(player, target);
        if (target is C2_TreeRow row && row._tree == _tree)
            _tree.CommitDrop(_node, row._node, row.DropKind(player));
        else _tree.ClearDrop();
    }

    // Reorder is the row-to-row drag inside a tree that owns on_item_drop; everything else
    // (another tree, a file thumbnail, a sibling row in a tree with no reorder) is a payload drop.
    bool IsReorder(ImpComp dropped)
    {
        return dropped is C2_TreeRow src && src._tree == _tree && _tree.on_item_drop != null;
    }

    public override void CursorGrab_HoveredAsTarget(ImpPlayer player, ImpComp dropped, bool hovered)
    {
        base.CursorGrab_HoveredAsTarget(player, dropped, hovered);
        bool reorder = IsReorder(dropped);
        bool external = !reorder && _tree.on_item_drop_external != null
            && dropped != this && dropped?.CursorGrab_Payload() != null;
        if (!reorder && !external) return;
        if (!hovered)
        {
            if (_tree.drop_node == _node) _tree.ClearDrop();
            return;
        }
        // Outside content always lands inside the row, never between rows.
        if (external) _tree.SetDrop(_node, ETreeDrop.Child);
        else ApplyDropHover(player);
    }

    public override void CursorGrab_DroppedOn(ImpPlayer player, ImpComp dropped)
    {
        base.CursorGrab_DroppedOn(player, dropped);
        if (IsReorder(dropped)) return; //the source row commits this one
        if (dropped == this) { _tree.ClearDrop(); return; }
        object payload = dropped?.CursorGrab_Payload();
        if (payload != null) _tree.CommitDropExternal(payload, _node);
    }

    void ApplyDropHover(ImpPlayer player)
    {
        _tree.SetDrop(_node, DropKind(player));
    }

    ETreeDrop DropKind(ImpPlayer player)
    {
        if (_node.parent == null) return ETreeDrop.Child;
        TDimensions2 dim = Dimensions_Get();
        float t = dim.size.Y > 0 ? (player.cursor.position.Y - dim.position.Y) / dim.size.Y : 0.5f;
        if (t < 0.28f) return ETreeDrop.Before;
        if (t > 0.72f) return ETreeDrop.After;
        return ETreeDrop.Child;
    }
}
