using System.Drawing;
using System.Numerics;
using ImperiumCore;
using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._2D;

// =====================================================================================================================
// Data model
// =====================================================================================================================

public class TreeItem
{
    public string           label          = "";
    public A_Texture2D?     icon;
    public ImpComponent2D?  left_widget;        // optional widget drawn in the left (label) column
    public ImpComponent2D?  widget;             // right-column value editor (null = use column_strings)
    public List<string>     column_strings = new();
    public List<TreeItem>   children       = new();
    public bool             expanded       = true;
    public bool             is_header;          // styled as a dark section header

    // Drop target support — set these instead of subclassing O2D_TreeRow.
    public bool             drop_enabled;
    public Func<object?, bool>? OnDropCanAccept;
    public Action<object?>? OnDrop;
}

// =====================================================================================================================
// Widget
// =====================================================================================================================

public class O2D_Tree : ImpComponent2D
{
    public List<TreeItem>       roots          = new();
    public float                label_ratio    = 0.42f;
    public float                row_height     = 24f;
    public float                indent_size    = 14f;
    public bool                 enable_DragDrop;
    public Action<TreeItem>?    OnItemSelect;
    public Action<TreeItem,bool>? OnItemExpand;

    private readonly List<O2D_TreeRow> _rows = new();

    // Call after modifying roots/children to rebuild the visible flat row list.
    public void Items_Refresh()
    {
        foreach (var r in _rows) Child_Remove(r);
        _rows.Clear();
        foreach (var root in roots) Row_Flatten(root, 0);
        if (_rect.size.X > 0 || _rect.size.Y > 0) Layout_Children(_rect);
    }

    private void Row_Flatten(TreeItem item, int depth)
    {
        var row = new O2D_TreeRow(item, depth, this);
        _rows.Add(row);
        Child_Add(row);
        if (item.expanded && item.children.Count > 0)
            foreach (var child in item.children) Row_Flatten(child, depth + 1);
    }

    protected override void Layout_Children(TRect2 rect)
    {
        float y = rect.position.Y;
        foreach (var row in _rows)
        {
            row.Layout_SetRect(new TRect2(new Vector2(rect.position.X, y), new Vector2(rect.size.X, row_height)));
            y += row_height;
        }
    }

    public override Vector2 Size_GetMinimum() =>
        Vector2.Max(new Vector2(0, _rows.Count * row_height), base.Size_GetMinimum());
}

// =====================================================================================================================
// Row (one visible entry in the tree)
// =====================================================================================================================

internal class O2D_TreeRow : ImpComponent2D
{
    internal readonly TreeItem item;
    private  readonly int      _depth;
    private  readonly O2D_Tree _tree;

    private const float ExpandW = 14f;
    private const float IconSz  = 14f;
    private const float Pad     = 6f;

    private const float HandleW = 22f;

    internal O2D_TreeRow(TreeItem item, int depth, O2D_Tree tree)
    {
        this.item = item;
        _depth    = depth;
        _tree     = tree;
        if (item.left_widget != null) Child_Add(item.left_widget);
        if (item.widget      != null) Child_Add(item.widget);
        if (item.drop_enabled) drop_enabled = true;
    }

    protected override void Layout_Children(TRect2 rect)
    {
        if (item.left_widget != null)
        {
            float lx = rect.position.X + _depth * _tree.indent_size;
            item.left_widget.Layout_SetRect(new TRect2(
                new Vector2(lx, rect.position.Y),
                new Vector2(HandleW, rect.size.Y)));
        }

        if (item.widget == null) return;
        float splitX = rect.position.X + rect.size.X * _tree.label_ratio;
        item.widget.Layout_SetRect(new TRect2(
            new Vector2(splitX + 2, rect.position.Y + 1),
            new Vector2(rect.size.X * (1f - _tree.label_ratio) - 4, rect.size.Y - 2)));
    }

    public override bool OnDropCanAccept(object? payload) =>
        item.drop_enabled && (item.OnDropCanAccept?.Invoke(payload) ?? true);

    public override void OnDrop(object? payload) => item.OnDrop?.Invoke(payload);

    public override void OnDraw(ImpRender rhi, double dt)
    {
        float h = _rect.size.Y;

        if (item.is_header)
            rhi.Draw2D_Rect(TVector2i.From(_rect.position), TVector2i.From(_rect.size),
                Color.FromArgb(38, 38, 44));
        else if (hovered)
            rhi.Draw2D_Rect(TVector2i.From(_rect.position), TVector2i.From(_rect.size),
                Color.FromArgb(45, 45, 52));

        // Drag insertion indicator
        var player = ImpApp.Active?.players.Count > 0 ? ImpApp.Active.players[0] : null;
        if (player is { IsDragging: true } && player.DragTarget == this)
            rhi.Draw2D_Rect(
                new TVector2i((int)_rect.position.X, (int)_rect.position.Y),
                new TVector2i((int)_rect.size.X, 2),
                Color.FromArgb(80, 140, 255));

        float leftShift = item.left_widget != null ? HandleW : 0f;
        float x  = _rect.position.X + _depth * _tree.indent_size + leftShift + Pad;
        float cy = _rect.position.Y + h * 0.5f;

        // expander arrow
        if (item.children.Count > 0)
        {
            string arrow = item.expanded ? "▾" : "▸";
            rhi.Draw2D_Text(new TVector2i((int)x, (int)(cy - 7)), arrow, 11, Color.FromArgb(160, 160, 170));
        }
        x += ExpandW;

        // icon
        if (item.icon != null)
        {
            rhi.Draw2D_Texture(item.icon,
                new TVector2i((int)x, (int)(cy - IconSz * 0.5f)),
                new TVector2i((int)IconSz, (int)IconSz), Color.White);
            x += IconSz + Pad;
        }

        // label (left column)
        if (!string.IsNullOrEmpty(item.label))
        {
            int  fs = item.is_header ? 13 : 12;
            var  lc = item.is_header ? Color.FromArgb(200, 200, 215) : Color.FromArgb(155, 155, 165);
            rhi.Draw2D_Text(new TVector2i((int)x, (int)(cy - 7)), item.label, fs, lc);
        }

        // row separator
        rhi.Draw2D_Rect(
            new TVector2i((int)_rect.position.X, (int)(_rect.position.Y + h - 1)),
            new TVector2i((int)_rect.size.X, 1),
            item.is_header ? Color.FromArgb(55, 55, 62) : Color.FromArgb(30, 30, 35));
    }

    public override void OnClicked()
    {
        if (item.children.Count > 0)
        {
            item.expanded = !item.expanded;
            _tree.OnItemExpand?.Invoke(item, item.expanded);
            _tree.Items_Refresh();
        }
        _tree.OnItemSelect?.Invoke(item);
    }
}
