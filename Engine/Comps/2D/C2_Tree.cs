using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// One row of a tree.
//
// A class rather than a struct: rows carry state the tree toggles through references it
// holds (expanded, selected), and a struct sitting in a List<> hands out copies, so those
// edits would land on the copy and vanish.
public class TTreeItem
{
    //whatever the row stands for - an ImpComp for the outliner, an asset elsewhere
    public object? data;

    public bool is_expanded = true;
    public bool is_selected;

    // Columns for the row. sections[0] is the label and takes the leftover width; the rest
    // are drawn dim, in fixed-width columns off the right edge.
    public List<string> sections = new List<string>();
    public List<TTreeItem> children = new List<TTreeItem>();

    public TTreeItem? parent;

    public string Label => sections.Count > 0 ? sections[0] : "";

    public void Clear()
    {
        data = null;
        is_expanded = true;
        is_selected = false;
        sections = new List<string>();
        children = new List<TTreeItem>();
    }

    public void Child_Add(TTreeItem child)
    {
        child.parent = this;
        children.Add(child);
    }
}

// A scrolling, collapsible tree.
//
// Rows are not comps. The whole tree is one hit-testable comp that flattens its items into
// a row list every layout and resolves clicks by looking up which row the cursor is over -
// a comp per row would mean rebuilding the subtree on every expand and would not survive a
// scene of any size.
public class C2_Tree : ImpComp2D
{
    public TTreeItem root_item = new TTreeItem();
    public bool allow_multiselect = false;

    //the root usually stands for the container itself (a scene), so it can be left out
    public bool show_root = true;

    public float indent = 14f;
    public float row_separation = 0f;
    public float section_width = 96f; //width of each column after the label

    public float scroll = 0f;
    public float scroll_speed = 40f;

    public UIStyle_Rect? style;

    public Action<C2_Tree, TTreeItem>? on_select;
    public Action<C2_Tree, TTreeItem>? on_deselect;

    public readonly List<TTreeItem> selected = new List<TTreeItem>();

    // One visible row, resolved to an absolute rect. Rebuilt every layout, so collapsed
    // rows simply stop existing and can be neither drawn nor clicked.
    struct TTreeRow
    {
        public TTreeItem item;
        public int depth;
        public Rectangle rect;
    }

    readonly List<TTreeRow> rows = new List<TTreeRow>();
    float content_len; //how tall the expanded rows are in total

    public C2_Tree()
    {
        clip_contents = true; //rows scroll under the edges, and must not be hit out there
    }

    // ---------------------------------------------------
    // build
    // ---------------------------------------------------

    public void Build_FromComp(ImpComp comp)
    {
        Select_Clear();

        root_item.Clear();
        AddComp(root_item, comp);
    }

    public void AddComp(TTreeItem item, ImpComp comp)
    {
        item.data = comp;
        item.sections = [Comp_Label(comp), comp.GetType().Name];

        foreach (var child in comp.children)
        {
            var child_item = new TTreeItem();
            AddComp(child_item, child);
            item.Child_Add(child_item);
        }
    }

    static string Comp_Label(ImpComp comp)
    {
        return string.IsNullOrEmpty(comp.name) ? comp.GetType().Name : comp.name;
    }

    // The item whose data is this object, or null. Lets a caller drive the tree's selection
    // from whatever it holds rather than having to keep item references around.
    public TTreeItem? Item_Find(object? data, TTreeItem? from = null)
    {
        if (data == null) return null;
        from ??= root_item;

        if (ReferenceEquals(from.data, data)) return from;

        foreach (var child in from.children)
        {
            var hit = Item_Find(data, child);
            if (hit != null) return hit;
        }

        return null;
    }

    // ---------------------------------------------------
    // selection
    // ---------------------------------------------------

    // Additive keeps what is already selected and toggles this one - what ctrl-click does.
    // Otherwise the selection collapses to just this item.
    public void Select(TTreeItem? item, bool additive = false)
    {
        if (!additive || !allow_multiselect) Select_Clear();
        if (item == null) return;

        //ctrl-clicking something already selected takes it back out
        Item_SetSelected(item, !item.is_selected);
    }

    public void Select_Clear()
    {
        for (int i = selected.Count - 1; i >= 0; i--) { Item_SetSelected(selected[i], false); }
    }

    void Item_SetSelected(TTreeItem item, bool value)
    {
        if (item.is_selected == value) return;
        item.is_selected = value;

        if (value)
        {
            selected.Add(item);
            on_select?.Invoke(this, item);
        }
        else
        {
            selected.Remove(item);
            on_deselect?.Invoke(this, item);
        }
    }

    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    public override Vector2 Size_GetContentMin()
    {
        return new Vector2(0, Rows_Count(root_item, show_root) * Row_Step());
    }

    protected override void Layout_Children(Rectangle content)
    {
        rows.Clear();

        var th = Theme_Get();
        float step = Row_Step();

        content_len = Rows_Count(root_item, show_root) * step;

        float overflow = MathF.Max(0f, content_len - content.Height);
        scroll = Math.Clamp(scroll, 0f, overflow);

        //the scrollbar eats into the row width only when there is something to scroll
        float width = content.Width - (overflow > 0f ? th.scrollbar_width : 0f);
        float y = content.Y - scroll;

        if (show_root)
        {
            Rows_Build(root_item, 0, content.X, width, step, th.item_height, ref y);
            return;
        }

        foreach (var child in root_item.children)
        {
            Rows_Build(child, 0, content.X, width, step, th.item_height, ref y);
        }
    }

    void Rows_Build(TTreeItem item, int depth, float x, float width, float step, float height, ref float y)
    {
        rows.Add(new TTreeRow
        {
            item = item,
            depth = depth,
            rect = new Rectangle(x, y, width, height),
        });
        y += step;

        if (!item.is_expanded) return;

        foreach (var child in item.children)
        {
            Rows_Build(child, depth + 1, x, width, step, height, ref y);
        }
    }

    static int Rows_Count(TTreeItem item, bool count_self)
    {
        int count = count_self ? 1 : 0;
        if (count_self && !item.is_expanded) return count;

        foreach (var child in item.children) { count += Rows_Count(child, true); }
        return count;
    }

    float Row_Step() => Theme_Get().item_height + row_separation;

    // The clickable arrow box for a row. Rows with no children have no arrow, but the space
    // is still reserved so labels at the same depth line up.
    Rectangle Rect_Arrow(TTreeRow row)
    {
        const float size = 10f;

        return new Rectangle(
            row.rect.X + row.depth * indent + (indent - size) * 0.5f,
            row.rect.Y + (row.rect.Height - size) * 0.5f,
            size, size);
    }

    int Row_At(Vector2 point)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(point, rows[i].rect)) return i;
        }
        return -1;
    }

    // ---------------------------------------------------
    // input
    // ---------------------------------------------------

    public override bool Cursor_WantsWheel() => true;

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        if (ev == ECursorEvent.Wheel)
        {
            scroll -= ImpUI.wheel * scroll_speed;
            return;
        }

        if (ev != ECursorEvent.Clicked) return;

        int index = Row_At(ImpUI.mouse_pos);
        if (index < 0)
        {
            Select_Clear(); //clicking the empty space below the rows drops the selection
            return;
        }

        var row = rows[index];

        // the arrow column expands; anywhere else on the row selects
        if (row.item.children.Count > 0 && Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, Rect_Arrow(row)))
        {
            row.item.is_expanded = !row.item.is_expanded;
            return;
        }

        Select(row.item, ImpPlayer.Key_IsDown(EInputKey.Key_LeftControl)
                      || ImpPlayer.Key_IsDown(EInputKey.Key_RightControl));
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        if (style != null) ImpUI.Rect(rect, style.color);

        //clip_contents only wraps the children pass, and the rows are drawn here
        ImpUI.Clip_Push(rect_content);

        int hot = ImpUI.IsHovered(this) ? Row_At(ImpUI.mouse_pos) : -1;

        float clip_top = rect_content.Y;
        float clip_bottom = rect_content.Y + rect_content.Height;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.rect.Y + row.rect.Height < clip_top) continue;
            if (row.rect.Y > clip_bottom) break; //rows are in order, so the rest are below too

            Row_Draw(row, i == hot, th);
        }

        ImpUI.Clip_Pop();

        Scrollbar_Draw(th);
    }

    void Row_Draw(TTreeRow row, bool is_hot, ImpUITheme th)
    {
        if (row.item.is_selected) ImpUI.Rect(row.rect, th.col_accent);
        else if (is_hot) ImpUI.Rect(row.rect, th.col_hover);

        if (row.item.children.Count > 0) Arrow_Draw(Rect_Arrow(row), row.item.is_expanded, th);

        float text_x = row.rect.X + (row.depth + 1) * indent + th.padding * 0.5f;
        float columns_w = MathF.Max(0, row.item.sections.Count - 1) * section_width;
        float label_w = row.rect.X + row.rect.Width - text_x - columns_w - th.padding;

        ImpUI.TextInRect(row.item.Label,
            new Rectangle(text_x, row.rect.Y, MathF.Max(0, label_w), row.rect.Height),
            th.style_text, 0f);

        for (int s = 1; s < row.item.sections.Count; s++)
        {
            var column = new Rectangle(
                row.rect.X + row.rect.Width - columns_w + (s - 1) * section_width,
                row.rect.Y,
                MathF.Max(0, section_width - th.padding),
                row.rect.Height);

            ImpUI.TextInRect(row.item.sections[s], column, th.style_text_dim, 0f);
        }
    }

    static void Arrow_Draw(Rectangle box, bool is_expanded, ImpUITheme th)
    {
        float cx = box.X + box.Width * 0.5f;
        float cy = box.Y + box.Height * 0.5f;
        float s = box.Width;

        if (is_expanded)
        {
            ImpUI.Line(new Vector2(cx - s * 0.4f, cy - s * 0.2f), new Vector2(cx, cy + s * 0.3f), 1.5f, th.col_text);
            ImpUI.Line(new Vector2(cx, cy + s * 0.3f), new Vector2(cx + s * 0.4f, cy - s * 0.2f), 1.5f, th.col_text);
        }
        else
        {
            ImpUI.Line(new Vector2(cx - s * 0.2f, cy - s * 0.4f), new Vector2(cx + s * 0.3f, cy), 1.5f, th.col_text);
            ImpUI.Line(new Vector2(cx + s * 0.3f, cy), new Vector2(cx - s * 0.2f, cy + s * 0.4f), 1.5f, th.col_text);
        }
    }

    void Scrollbar_Draw(ImpUITheme th)
    {
        float overflow = MathF.Max(0f, content_len - rect_content.Height);
        if (overflow <= 0f) return;

        var track = new Rectangle(
            rect_content.X + rect_content.Width - th.scrollbar_width,
            rect_content.Y,
            th.scrollbar_width,
            rect_content.Height);

        ImpUI.Rect(track, th.col_panel);

        float grip_h = MathF.Max(24f, track.Height * (rect_content.Height / content_len));
        float t = scroll / overflow;

        ImpUI.Rect(new Rectangle(
            track.X + 2f,
            track.Y + (track.Height - grip_h) * t,
            track.Width - 4f,
            grip_h), th.col_line);
    }
}
