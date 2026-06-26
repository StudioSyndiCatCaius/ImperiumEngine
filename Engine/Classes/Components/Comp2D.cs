using System.Numerics;
using ImperiumCore.Assets;
using ImperiumCore.Enums;
using ImperiumCore.Structs;
using Silk.NET.Input;

namespace ImperiumCore.Classes.Components;

// #####################################################################################################################
// 2D
// #####################################################################################################################

// Fusion of Godot's CanvasItem(2D) + Control: a 2D node that lays itself out via
// the anchor/offset model and resolves its rect relative to its parent's.
public class ImpComponent2D : ImpComponent
{
    [ImpVar][Exposed] public TTransform2D transform;
    [ImpVar][Exposed] public A_2DTheme override_theme;

    // each side: anchor is a 0..1 fraction of the parent rect, offset is pixels
    [ImpVar] public float anchor_left, anchor_top, anchor_right, anchor_bottom;
    [ImpVar] public float offset_left, offset_top, offset_right, offset_bottom;

    [ImpVar][Exposed] public Vector2 custom_minimum_size;
    [ImpVar][Exposed] public ESizeFlags size_flags_h = ESizeFlags.Fill;
    [ImpVar][Exposed] public ESizeFlags size_flags_v = ESizeFlags.Fill;
    [ImpVar][Exposed] public float stretch_ratio = 1f;

    // how this node lays out its children
    [ImpVar][Exposed] public ELayoutMode layout_mode = ELayoutMode.Free;
    [ImpVar][Exposed] public float separation = 4f;

    [ImpVar][Exposed] public EMouseFilter mouse_filter = EMouseFilter.Stop;
    public bool hovered, pressed, focused; // runtime interaction state

    protected TRect2 _rect;

    public TRect2 Rect_Get() => _rect;
    public Vector2 Position_Get() => _rect.position;
    public Vector2 Size_Get() => _rect.size;

    // -------------------------------------------------------
    // Mouse interaction (per-node hooks, driven by the input dispatch)
    // -------------------------------------------------------

    public virtual void OnMouseEnter() { }
    public virtual void OnMouseExit() { }
    public virtual void OnMousePressed(int button) { }
    public virtual void OnMouseReleased(int button) { }
    public virtual void OnClicked() { }

    public virtual void OnFocus()           { }
    public virtual void OnUnfocus()         { }
    public virtual void OnKeyChar(char c)   { }
    public virtual void OnKeyDown(Key key)  { }

    // -------------------------------------------------------
    // Drag & Drop
    // -------------------------------------------------------

    public bool drop_enabled;

    // Return false to reject a payload before OnDrop is called.
    public virtual bool OnDropCanAccept(object? payload) => false;
    public virtual void OnDragEnter(object? payload)     { }
    public virtual void OnDragExit(object? payload)      { }
    public virtual void OnDrop(object? payload)          { }
    // Called on the drag SOURCE when the drag ends (dropped = whether a target accepted it).
    public virtual void OnDragEnd(bool dropped)          { }

    // topmost node (deepest child first) under point p, respecting mouse_filter
    public ImpComponent2D? Hit_Test(Vector2 p)
    {
        if (!visible) return null;

        for (int i = _children.Count - 1; i >= 0; i--)
        {
            if (_children[i] is ImpComponent2D c)
            {
                var hit = c.Hit_Test(p);
                if (hit != null) return hit;
            }
        }

        return (mouse_filter == EMouseFilter.Stop && _rect.Contains(p)) ? this : null;
    }

    public override void Component_Layout(TRect2 parentRect)
    {
        _rect = Rect_Compute(parentRect);
        Layout_Children(_rect);
    }

    // assign this node's rect directly (used by containers that manage their children),
    // then lay out its own subtree
    public void Layout_SetRect(TRect2 rect)
    {
        _rect = rect;
        Layout_Children(_rect);
    }

    // Free: children resolve against this rect via their own anchors/offsets.
    // Vertical/Horizontal: box-arrange children. Specialized containers override.
    protected virtual void Layout_Children(TRect2 rect)
    {
        if (layout_mode == ELayoutMode.Free)
        {
            foreach (var c in _children) c.Component_Layout(rect);
            return;
        }
        Layout_Box(rect, layout_mode == ELayoutMode.Vertical);
    }

    // arrange children along one axis, honoring min sizes, Expand/Fill flags and stretch_ratio
    private void Layout_Box(TRect2 rect, bool vertical)
    {
        var kids = new List<ImpComponent2D>();
        foreach (var c in _children)
        {
            if (c is ImpComponent2D c2)
            {
                if (c2.visible) kids.Add(c2);
            }
            else
            {
                c.Component_Layout(rect);
            }
        }
        if (kids.Count == 0) return;

        float totalMin = separation * (kids.Count - 1);
        float expandSum = 0f;
        foreach (var k in kids)
        {
            totalMin += Axis(k.Size_GetMinimum(), vertical);
            if (HasExpand(k, vertical)) expandSum += k.stretch_ratio;
        }

        float extra = MathF.Max(0f, (vertical ? rect.size.Y : rect.size.X) - totalMin);

        float main = vertical ? rect.position.Y : rect.position.X;
        float crossOrigin = vertical ? rect.position.X : rect.position.Y;
        float crossFull = vertical ? rect.size.X : rect.size.Y;

        foreach (var k in kids)
        {
            var min = k.Size_GetMinimum();

            float mainSize = Axis(min, vertical);
            if (expandSum > 0f && HasExpand(k, vertical))
                mainSize += extra * (k.stretch_ratio / expandSum);

            float crossSize = HasFillCross(k, vertical) ? crossFull : Axis(min, !vertical);

            k.Layout_SetRect(vertical
                ? new TRect2(new Vector2(crossOrigin, main), new Vector2(crossSize, mainSize))
                : new TRect2(new Vector2(main, crossOrigin), new Vector2(mainSize, crossSize)));

            main += mainSize + separation;
        }
    }

    public virtual Vector2 Size_GetMinimum()
    {
        var own = Vector2.Max(custom_minimum_size, Vector2.Zero);
        if (layout_mode == ELayoutMode.Free) return own;

        bool vertical = layout_mode == ELayoutMode.Vertical;
        float main = 0f, cross = 0f;
        int count = 0;
        foreach (var c in _children)
        {
            if (c is ImpComponent2D c2 && c2.visible)
            {
                var m = c2.Size_GetMinimum();
                main += Axis(m, vertical);
                cross = MathF.Max(cross, Axis(m, !vertical));
                count++;
            }
        }
        if (count > 1) main += separation * (count - 1);

        var content = vertical ? new Vector2(cross, main) : new Vector2(main, cross);
        return Vector2.Max(content, own);
    }

    private static float Axis(Vector2 v, bool vertical) => vertical ? v.Y : v.X;

    private static bool HasExpand(ImpComponent2D c, bool vertical) =>
        ((vertical ? c.size_flags_v : c.size_flags_h) & ESizeFlags.Expand) != 0;

    private static bool HasFillCross(ImpComponent2D c, bool vertical) =>
        ((vertical ? c.size_flags_h : c.size_flags_v) & ESizeFlags.Fill) != 0;

    protected TRect2 Rect_Compute(TRect2 parent)
    {
        float l = parent.position.X + anchor_left   * parent.size.X + offset_left;
        float t = parent.position.Y + anchor_top    * parent.size.Y + offset_top;
        float r = parent.position.X + anchor_right  * parent.size.X + offset_right;
        float b = parent.position.Y + anchor_bottom * parent.size.Y + offset_bottom;

        var size = Vector2.Max(new Vector2(r - l, b - t), Size_GetMinimum());
        return new TRect2(new Vector2(l, t), size);
    }

    // place at a fixed position/size (anchors to top-left)
    public void Rect_Set(Vector2 position, Vector2 size)
    {
        anchor_left = anchor_top = anchor_right = anchor_bottom = 0;
        offset_left = position.X;
        offset_top = position.Y;
        offset_right = position.X + size.X;
        offset_bottom = position.Y + size.Y;
    }

    public void Anchors_Preset(EAnchorPreset preset)
    {
        (anchor_left, anchor_top, anchor_right, anchor_bottom) = preset switch
        {
            EAnchorPreset.TopLeft      => (0f, 0f, 0f, 0f),
            EAnchorPreset.TopRight     => (1f, 0f, 1f, 0f),
            EAnchorPreset.BottomLeft   => (0f, 1f, 0f, 1f),
            EAnchorPreset.BottomRight  => (1f, 1f, 1f, 1f),
            EAnchorPreset.CenterLeft   => (0f, 0.5f, 0f, 0.5f),
            EAnchorPreset.CenterTop    => (0.5f, 0f, 0.5f, 0f),
            EAnchorPreset.CenterRight  => (1f, 0.5f, 1f, 0.5f),
            EAnchorPreset.CenterBottom => (0.5f, 1f, 0.5f, 1f),
            EAnchorPreset.Center       => (0.5f, 0.5f, 0.5f, 0.5f),
            EAnchorPreset.LeftWide     => (0f, 0f, 0f, 1f),
            EAnchorPreset.TopWide      => (0f, 0f, 1f, 0f),
            EAnchorPreset.RightWide    => (1f, 0f, 1f, 1f),
            EAnchorPreset.BottomWide   => (0f, 1f, 1f, 1f),
            EAnchorPreset.VCenterWide  => (0f, 0.5f, 1f, 0.5f),
            EAnchorPreset.HCenterWide  => (0.5f, 0f, 0.5f, 1f),
            EAnchorPreset.FullRect     => (0f, 0f, 1f, 1f),
            _                          => (anchor_left, anchor_top, anchor_right, anchor_bottom),
        };

        if (preset is EAnchorPreset.FullRect or EAnchorPreset.LeftWide or EAnchorPreset.TopWide
            or EAnchorPreset.RightWide or EAnchorPreset.BottomWide
            or EAnchorPreset.VCenterWide or EAnchorPreset.HCenterWide)
        {
            offset_left = offset_top = offset_right = offset_bottom = 0;
        }
    }
}

