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
    public bool is_disabled;
    //Washed faintly across the whole row. Left at alpha 0 the row draws with the tree's own colours.
    public Color row_tint;
}

public struct TTreeItemSection
{
    public string text;
    public A_Texture icon;
    //Stands in for icon while the node is expanded. Null keeps icon either way.
    public A_Texture icon_expanded;
    //Live GPU texture (png preview, etc). When set, drawn instead of icon.
    public Texture2D? icon_texture;
    public Color color;
    //Multiplied into the icon. Alpha 0 leaves it white. Ignored for icon_texture.
    public Color icon_tint;
}

public enum ETreeDrop { Before, After, Child }

public class C2_Tree : Imp2D
{
    public UI_Box box = UI_Box.BkgDark;
    public UI_Text Text = UI_Text.LIGHT;
    public UI_Text TextSelected = UI_Text.LIGHT;
    public Color color_row = new(40, 40, 40, 0);
    public Color color_row_hover = new(70, 70, 74, 255);
    public Color color_row_selected = new(0, 96, 166, 255);
    public float row_height = 22f;
    public float indent_size = 16f;
    public float icon_size = 14f;

    public Action<TTreeItem> on_item_click;
    public Action<TTreeItem> on_item_right_click;
    public Action<TTreeItem> on_item_double_click;
    public Action on_background_right_click;
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
        layout.orient_H = EUIViewportAlignment.Fill;
        layout.orient_V = EUIViewportAlignment.Fill;
        EnsureScroll();
    }

    void EnsureScroll()
    {
        if (_scroll != null) return;
        _scroll = new C2_ScrollBox
        {
            orentation = EUIOrentation.V,
            style = box,
            layout = new TLayout2
                {
                    orient_H = EUIViewportAlignment.Fill,
                    orient_V = EUIViewportAlignment.Fill,
                },
        };
        Child_Add(_scroll);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        EnsureScroll();
        if (!children.Contains(_scroll)) Child_Add(_scroll);
        _scroll.style = box;

        if (on_background_right_click != null
            && ImpPlayer.Key_IsPressed(EInputKey.Mouse_Right)
            && ImpPlayer.players.Count > 0)
        {
            ImpPlayer p = ImpPlayer.players[0];
            if (p.Cursor_IsInDimensions(Dimensions_Get()) && p.target_cursor is not C2_TreeRow)
                on_background_right_click.Invoke();
        }
    }

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (box != null) box.Draw(Dimensions_Get());
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

    public void Tree_AddAt(int index, TTreeItem item)
    {
        if (index < 0)
        {
            index = 0;
        }
        if (index > _roots.Count)
        {
            index = _roots.Count;
        }
        _roots.Insert(index, ToNode(item, null));
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

    public List<string> Tree_ExpandedKeys()
    {
        return new List<string>(_expanded);
    }

    public void Tree_SetExpandedKeys(IEnumerable<string> keys)
    {
        _expanded.Clear();
        if (keys != null)
        {
            foreach (string k in keys)
            {
                if (!string.IsNullOrEmpty(k))
                {
                    _expanded.Add(k);
                }
            }
        }
        RebuildRows();
    }

    public void Tree_SelectData(object data)
    {
        if (data == null)
        {
            _selected_node = null;
            selected_data = null;
            selected_item = default;
            return;
        }
        TreeNode found = null;
        void Walk(TreeNode n)
        {
            if (found != null) return;
            if (DataEquals(n.item.data, data)) found = n;
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        for (int i = 0; i < _roots.Count; i++) Walk(_roots[i]);
        if (found != null) SelectNode(found, false);
    }

    /// <summary>
    /// Where the row carrying this data currently sits on screen. False when nothing holds it, or
    /// when it is scrolled out of view - only expanded nodes have a row at all.
    /// </summary>
    public bool Row_Rect(object data, out TDimensions2 dim)
    {
        dim = default;
        if (data == null || _scroll == null) return false;
        List<ImpComp> rows = _scroll.children;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] is not C2_TreeRow row || !DataEquals(row.Data, data)) continue;
            dim = row.Dimensions_Get();
            return dim.size.X > 0 && dim.size.Y > 0;
        }
        return false;
    }

    // Tree data is mostly path structs, and two TDirectory/TFile values naming the same place are
    // the same node even when the strings differ in casing or separators.
    static bool DataEquals(object a, object b)
    {
        if (a is TDirectory da && b is TDirectory db) return PathsEqual(da.path, db.path);
        if (a is TFile fa && b is TFile fb) return PathsEqual(fa.path, fb.path);
        return Equals(a, b);
    }

    public void Tree_Populate_FromComp(ImpComp comp)
    {
        Tree_Clear();
        if (comp == null) return;
        Tree_Add(FromComp(comp, false));
        Tree_ExpandKey(KeyOf(comp), true);
    }

    public void Tree_Populate_Components(ImpComp host)
    {
        Tree_Clear();
        if (host == null) return;
        Tree_Add(FromComp(host, true));
        Tree_ExpandKey(KeyOf(host), true);
        Tree_ExpandAll(true);
    }

    Type _class_root;
    Dictionary<Type, List<Type>> _class_kids;
    List<Type> _class_roots;
    string _class_query = "";
    static readonly Dictionary<string, A_Texture> _class_icons = new();

    public void Tree_Populate_FromClasses(Type type)
    {
        _class_root = type;
        _class_query = "";
        _class_kids = new Dictionary<Type, List<Type>>();
        _class_roots = new List<Type>();
        if (type == null)
        {
            Tree_Clear();
            return;
        }

        List<Type> types = new();
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (IsEditorAssembly(asm)) continue;
            Type[] found;
            try { found = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { found = ex.Types.Where(t => t != null).ToArray()!; }
            foreach (Type t in found)
            {
                if (t == null || !t.IsClass || t.ContainsGenericParameters) continue;
                if (!type.IsAssignableFrom(t)) continue;
                if (Class_IsHidden(t)) continue;
                types.Add(t);
            }
        }
        if (type.IsClass && !Class_IsHidden(type) && !IsEditorAssembly(type.Assembly) && !types.Contains(type))
            types.Add(type);

        Comparison<Type> by_name = (a, b) => string.Compare(Class_DisplayName(a), Class_DisplayName(b), StringComparison.OrdinalIgnoreCase);
        types.Sort(by_name);

        for (int i = 0; i < types.Count; i++) _class_kids[types[i]] = new List<Type>();
        for (int i = 0; i < types.Count; i++)
        {
            Type t = types[i];
            Type parent = t.BaseType;
            while (parent != null && !_class_kids.ContainsKey(parent))
                parent = parent.BaseType;
            if (parent != null && parent != t)
                _class_kids[parent].Add(t);
            else
                _class_roots.Add(t);
        }

        ApplyClassFilter("");
    }

    public void Tree_FilterClasses(string query)
    {
        ApplyClassFilter(query);
    }

    void ApplyClassFilter(string query)
    {
        _class_query = query?.Trim() ?? "";
        if (_class_kids == null || _class_roots == null)
        {
            Tree_Clear();
            return;
        }

        object keep = selected_data;
        bool any = _class_query.Length > 0;

        bool Matches(Type t) =>
            !any
            || t.Name.Contains(_class_query, StringComparison.OrdinalIgnoreCase)
            || Class_DisplayName(t).Contains(_class_query, StringComparison.OrdinalIgnoreCase);

        bool Visible(Type t)
        {
            if (Matches(t)) return true;
            List<Type> c = _class_kids[t];
            for (int i = 0; i < c.Count; i++)
                if (Visible(c[i])) return true;
            return false;
        }

        TTreeItem Build(Type t, bool take_all)
        {
            List<Type> c = _class_kids[t];
            List<TTreeItem> kids = new();
            bool self = take_all || Matches(t);
            for (int i = 0; i < c.Count; i++)
            {
                if (self || Visible(c[i]))
                    kids.Add(Build(c[i], self));
            }
            bool abs = t.IsAbstract;
            return new TTreeItem
            {
                sections = new[]
                {
                    new TTreeItemSection
                    {
                        text = Class_DisplayName(t),
                        icon = Class_Icon(t),
                        color = abs ? new Color(140, 140, 140, 255) : default,
                    }
                },
                data = t,
                children = kids.ToArray(),
                is_disabled = abs,
            };
        }

        List<TTreeItem> items = new();
        for (int i = 0; i < _class_roots.Count; i++)
            if (!any || Visible(_class_roots[i]))
                items.Add(Build(_class_roots[i], false));
        Tree_SetItems(items);
        Tree_ExpandAll(true);
        if (keep != null) Tree_SelectData(keep);
    }

    public static bool Class_IsHidden(Type type)
    {
        for (Type n = type; n != null; n = n.BaseType)
        {
            ImpClassAttribute attr = n.GetCustomAttribute<ImpClassAttribute>(false);
            if (attr != null && attr.Hidden) return true;
        }
        return false;
    }

    public static bool Class_IsCommon(Type type)
    {
        if (type == null)
        {
            return false;
        }
        ImpClassAttribute attr = type.GetCustomAttribute<ImpClassAttribute>(false);
        return attr != null && attr.Common;
    }

    public static bool IsEditorAssembly(Assembly asm)
    {
        string n = asm?.GetName().Name;
        return n != null && n.Equals("Editor", StringComparison.OrdinalIgnoreCase);
    }

    public static string Class_DisplayName(Type type)
    {
        string n = type?.Name ?? "";
        if (n.Length > 3 && n[0] == 'C' && n[2] == '_' && (n[1] is '1' or '2' or '3'))
            return n[3..];
        return n;
    }

    public static readonly Color COLOR_INSTANCE = new(236, 196, 82, 255);
    public static readonly Color COLOR_OWNED = new(140, 180, 220, 255);

    public static Color Comp_Tint(ImpComp comp)
    {
        if (comp == null)
        {
            return default;
        }
        if (comp.IsInstanceRoot || comp.IsPackedForeign)
        {
            return COLOR_INSTANCE;
        }
        if (comp.IsOwned)
        {
            return COLOR_OWNED;
        }
        return default;
    }

    public static A_Texture Class_Icon(Type type)
    {
        if (type == null) return A_Texture.ICO_COMP;
        string key = type.FullName ?? type.Name;
        if (_class_icons.TryGetValue(key, out A_Texture hit)) return hit;

        static A_Texture TryFile(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string[] folders = { "{engine}/Icons/type", "{engine}/Icons/Types" };
            for (int f = 0; f < folders.Length; f++)
            {
                string path = folders[f] + "/" + name + ".png";
                if (!File.Exists(ImpFile.Path_Resolve(path))) continue;
                A_Texture tex = ImpAsset.Import<A_Texture>(path);
                if (tex != null && tex.texture.Id != 0) return tex;
            }
            return null;
        }

        A_Texture found = null;
        for (Type n = type; n != null && found == null; n = n.BaseType)
        {
            found = TryFile(n.Name) ?? TryFile(Class_DisplayName(n));
            if (n == typeof(ImpComp)) break;
        }
        if (found == null)
        {
            if (typeof(Imp3D).IsAssignableFrom(type)) found = A_Texture.ICO_COMP3D;
            else if (typeof(Imp2D).IsAssignableFrom(type)) found = A_Texture.ICO_COMP2D;
            else found = A_Texture.ICO_COMP;
        }
        _class_icons[key] = found;
        return found;
    }

    TTreeItem FromComp(ImpComp comp, bool components)
    {
        List<TTreeItem> kid_list = new();
        for (int i = 0; i < comp.children.Count; i++)
        {
            ImpComp child = comp.children[i];
            if (child == null)
            {
                continue;
            }
            if (components)
            {
                if (!CompTree_Include(comp, child))
                {
                    continue;
                }
            }
            else if (child.IsOwned || child.IsPackedForeign)
            {
                continue;
            }
            kid_list.Add(FromComp(child, components));
        }
        A_Texture icon = Class_Icon(comp.GetType());
        Color name_col = Comp_Tint(comp);
        if (comp.IsInstanceRoot)
        {
            _expanded.Add(KeyOf(comp));
        }
        string label = CompTree_Label(comp, components);
        return new TTreeItem
        {
            sections = new[]
            {
                new TTreeItemSection
                {
                    text = label,
                    icon = icon,
                    color = name_col,
                }
            },
            children = kid_list.ToArray(),
            data = comp,
        };
    }

    static string CompTree_Label(ImpComp comp, bool components)
    {
        if (components && comp.parent != null)
        {
            FieldInfo slot = comp.parent.OwnedFieldOf(comp);
            if (slot != null && slot.IsPublic)
            {
                return slot.Name;
            }
        }
        if (string.IsNullOrEmpty(comp.name))
        {
            return comp.GetType().Name;
        }
        return comp.name;
    }

    static bool CompTree_Include(ImpComp parent, ImpComp child)
    {
        if (child.IsPackedForeign)
        {
            return true;
        }
        FieldInfo slot = parent.OwnedFieldOf(child);
        if (slot != null)
        {
            return slot.IsPublic;
        }
        if (parent.IsOwned || parent.IsPackedForeign || parent.IsInstanceRoot)
        {
            return true;
        }
        return false;
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
                layout = new TLayout2
                {
                    size = new Vector2(0, row_height),
                    size_min = new Vector2(0, row_height),
                    orient_H = EUIViewportAlignment.Fill,
                },
            };
            _scroll.Child_Add(row);
        }

        // Cursor / expand can rebuild after Update has already laid the scroll box out.
        // Place the new rows now so this frame does not draw them stacked at the origin.
        TDimensions2 sdim = _scroll.Dimensions_Get();
        _scroll.content_length = C2_List.LayoutMainAxis(
            _scroll.children, false, sdim.size.Y, _scroll.spacing, _scroll.scroll);
        float max_scroll = MathF.Max(0, _scroll.content_length - sdim.size.Y);
        if (_scroll.scroll > max_scroll)
        {
            _scroll.scroll = max_scroll;
            _scroll.content_length = C2_List.LayoutMainAxis(
                _scroll.children, false, sdim.size.Y, _scroll.spacing, _scroll.scroll);
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
        if (node != null && node.item.is_disabled)
        {
            if (fire) return;
        }
        _selected_node = node;
        selected_item = node.item;
        selected_data = node.item.data;
        if (fire) on_item_click?.Invoke(node.item);
    }

    internal void OpenNode(TreeNode node)
    {
        if (node != null && node.item.is_disabled) return;
        on_item_double_click?.Invoke(node.item);
    }

    internal void RightClickNode(TreeNode node)
    {
        if (node != null && node.item.is_disabled) return;
        _selected_node = node;
        selected_item = node.item;
        selected_data = node.item.data;
        on_item_right_click?.Invoke(node.item);
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
        public double last_click = -999;
    }
}
[ImpClass(Hidden = true)]
class C2_TreeRow : Imp2D
{
    readonly C2_Tree _tree;
    readonly C2_Tree.TreeNode _node;
    readonly int _depth;
    bool _hover;

    public C2_TreeRow(C2_Tree tree, C2_Tree.TreeNode node, int depth)
    {
        _tree = tree;
        _node = node;
        _depth = depth;
        cursor_filter = ECursorFilter.Hit;
        option_button = null;
    }

    internal object Data => _node.item.data;

    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        TDimensions2 dim = Dimensions_Get();
        if (dim.size.X <= 0 || dim.size.Y <= 0) return;

        bool disabled = _node.item.is_disabled;
        bool selected = !disabled && _tree.IsSelected(_node);
        Color bg = selected
            ? _tree.color_row_selected
            : (_hover && !disabled ? _tree.color_row_hover : _tree.color_row);
        if (bg.A > 0) Raylib.DrawRectangleV(dim.position, dim.size, bg);

        // Folder colour washes under everything but the selection, which has to stay readable.
        Color wash = _node.item.row_tint;
        if (wash.A > 0 && !selected)
            Raylib.DrawRectangleV(dim.position, dim.size, new Color(wash.R, wash.G, wash.B, (byte)30));

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
                Texture2D tex = default;
                bool have_tex = false;
                Color ic = Color.White;
                if (s.icon_texture.HasValue && s.icon_texture.Value.Id != 0)
                {
                    tex = s.icon_texture.Value;
                    have_tex = true;
                }
                else
                {
                    A_Texture ico = s.icon;
                    if (_node.expanded && s.icon_expanded != null)
                    {
                        ico = s.icon_expanded;
                    }
                    if (ico != null && ico.texture.Id != 0)
                    {
                        tex = ico.texture;
                        have_tex = true;
                        if (s.icon_tint.A > 0)
                        {
                            ic = s.icon_tint;
                        }
                    }
                }
                if (have_tex)
                {
                    float sz = _tree.icon_size;
                    if (disabled)
                    {
                        ic = new Color(ic.R, ic.G, ic.B, (byte)90);
                    }
                    Raylib.DrawTexturePro(tex,
                        new Rectangle(0, 0, tex.Width, tex.Height),
                        new Rectangle(x, mid_y - sz * 0.5f, sz, sz),
                        Vector2.Zero, 0f, ic);
                    x += sz + 4;
                }
                if (!string.IsNullOrEmpty(s.text))
                {
                    UI_Text style = _tree.IsSelected(_node) ? _tree.TextSelected : _tree.Text;
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

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin) _hover = true;
        else if (notify == ENotifyGeneric.End) _hover = false;
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

        if (_node.item.is_disabled)
        {
            if (_node.children.Count > 0) _tree.ToggleExpand(_node);
            return;
        }

        double now = Raylib.GetTime();
        bool dbl = now - _node.last_click < 0.35;
        _node.last_click = now;
        _tree.SelectNode(_node, true);
        if (dbl)
        {
            _tree.OpenNode(_node);
        }
    }

    public override bool CursorGrab_IsEnabled(ImpPlayer player)
    {
        if (CursorGrab_Payload() != null) return true;
        return _tree.allow_reorder && _tree.on_item_drop != null
            && _node.item.data is ImpComp c && !c.IsPackedForeign && !c.IsOwned;
    }

    public override object CursorGrab_Payload()
    {
        return _tree.item_drag_payload?.Invoke(_node.item);
    }

    public override void _Notify_AsGrabbedTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsGrabbedTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Begin)
            _tree.SelectNode(_node, false);
        else if (notify == ENotifyGeneric.Update)
        {
            if (player.target_cursor is C2_TreeRow row && row._tree == _tree)
                row.ApplyDropHover(player);
            else _tree.ClearDrop();
        }
    }

    // Reorder is the row-to-row drag inside a tree that owns on_item_drop; everything else
    // (another tree, a file thumbnail, a sibling row in a tree with no reorder) is a payload drop.
    bool IsReorder(ImpComp dropped)
    {
        return dropped is C2_TreeRow src && src._tree == _tree && _tree.on_item_drop != null;
    }

    public override void _Notify_OnGrabDrop(ImpPlayer player, ENotifyGrabTarget notify, ImpComp other, double dt)
    {
        base._Notify_OnGrabDrop(player, notify, other, dt);
        if (notify == ENotifyGrabTarget.Drop_AsTarget)
        {
            if (other is C2_TreeRow row && row._tree == _tree)
                _tree.CommitDrop(_node, row._node, row.DropKind(player));
            else _tree.ClearDrop();
            return;
        }
        if (notify == ENotifyGrabTarget.Hover_AsInstigator_Start || notify == ENotifyGrabTarget.Hover_AsInstigator_End)
        {
            bool hovered = notify == ENotifyGrabTarget.Hover_AsInstigator_Start;
            bool reorder = IsReorder(other);
            bool external = !reorder && _tree.on_item_drop_external != null
                && other != this && other?.CursorGrab_Payload() != null;
            if (!reorder && !external) return;
            if (!hovered)
            {
                if (_tree.drop_node == _node) _tree.ClearDrop();
                return;
            }
            if (external) _tree.SetDrop(_node, ETreeDrop.Child);
            else ApplyDropHover(player);
            return;
        }
        if (notify == ENotifyGrabTarget.Drop_AsInstigator)
        {
            if (IsReorder(other)) return;
            if (other == this) { _tree.ClearDrop(); return; }
            object payload = other?.CursorGrab_Payload();
            if (payload != null) _tree.CommitDropExternal(payload, _node);
        }
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
