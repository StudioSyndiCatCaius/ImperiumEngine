using System.Drawing;
using System.Numerics;
using Hexa.NET.ImGui;
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

    public A_Texture2D t_ico_drop_closed = null;
    public A_Texture2D t_ico_drop_open = null;

    private readonly List<O2D_TreeRow> _rows = new();
    
    public O2D_Tree()
    {
        t_ico_drop_closed=ImpAsset.Load<A_Texture2D>("T_ico_Drop_0");
        t_ico_drop_open=ImpAsset.Load<A_Texture2D>("T_ico_Drop_1");
    }

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
        var dl  = ImGui.GetWindowDrawList();
        var pos = _rect.position;
        var sz  = _rect.size;
        float cy = pos.Y + sz.Y * 0.5f;

        if (item.is_header)
            dl.AddRectFilled(pos, pos + sz, ToU32(Color.FromArgb(38, 38, 44)));
        else if (hovered)
            dl.AddRectFilled(pos, pos + sz, ToU32(Color.FromArgb(45, 45, 52)));

        // Drag insertion indicator
        var player = ImpApp.Active?.players.Count > 0 ? ImpApp.Active.players[0] : null;
        if (player is { IsDragging: true } && player.DragTarget == this)
            dl.AddRectFilled(pos, new Vector2(pos.X + sz.X, pos.Y + 2), ToU32(Color.FromArgb(80, 140, 255)));

        float leftShift = item.left_widget != null ? HandleW : 0f;
        float x = pos.X + _depth * _tree.indent_size + leftShift + Pad;

        // Dropdown icon
        if (item.children.Count > 0)
        {
            var dropIcon = item.expanded ? _tree.t_ico_drop_open : _tree.t_ico_drop_closed;
            if (dropIcon != null)
            {
                rhi.Draw2D_Image(dropIcon,
                    new Vector2(x, cy - IconSz * 0.5f),
                    new Vector2(IconSz, IconSz),
                    Color.FromArgb(160, 160, 170));
            }
            else
            {
                string ch = item.expanded ? "▾" : "▸";
                var ts = ImGui.CalcTextSize(ch);
                dl.AddText(new Vector2(x, cy - ts.Y * 0.5f), ToU32(Color.FromArgb(160, 160, 170)), ch);
            }
        }
        x += ExpandW;

        if (item.icon != null)
        {
            rhi.Draw2D_Image(item.icon,
                new Vector2(x, cy - IconSz * 0.5f),
                new Vector2(IconSz, IconSz),
                Color.White);
            x += IconSz + Pad;
        }

        // Label (left column)
        if (!string.IsNullOrEmpty(item.label))
        {
            uint lc = item.is_header
                ? ToU32(Color.FromArgb(200, 200, 215))
                : ToU32(Color.FromArgb(155, 155, 165));
            var ts = ImGui.CalcTextSize(item.label);
            dl.AddText(new Vector2(x, cy - ts.Y * 0.5f), lc, item.label);
        }

        // Row separator
        uint sepCol = item.is_header ? ToU32(Color.FromArgb(55, 55, 62)) : ToU32(Color.FromArgb(30, 30, 35));
        dl.AddLine(new Vector2(pos.X, pos.Y + sz.Y - 1), new Vector2(pos.X + sz.X, pos.Y + sz.Y - 1), sepCol);
    }

    private static uint ToU32(Color c) => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);

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
