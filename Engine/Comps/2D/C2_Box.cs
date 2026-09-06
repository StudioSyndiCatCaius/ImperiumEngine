using System.Numerics;
using Engine.Core;
using Engine.Structs;
using Engine.Assets;
using Engine.Enums;
using Engine.Globals;
using Raylib_cs;

namespace Engine.Comps._2D;

public enum EBoxFormat { Stacked, Horizontal, Vertical, }

/*
 * Generic non-free container for other Imp2D components.
 */
[ImpClass(Common = true)][Title("Box")]
public class C2_Box : Imp2D
{
    // =============================================================================
    // ImpVars
    // =============================================================================
    [ImpVar] public UI_Box style=UI_Box.DARK; //if null, blank background
    [ImpVar] public EBoxFormat box_format;

    TBounds2[] _child_slots = Array.Empty<TBounds2>();
    bool _slots_dirty = true;

    // =============================================================================
    // INIT
    // =============================================================================
    public C2_Box()
    {
        cursor_filter = ECursorFilter.Hit;
    }
    
    public C2_Box(IEnumerable<ImpComp> _childs, EBoxFormat format=EBoxFormat.Vertical)
    {
        box_format = format;
        foreach (ImpComp child in _childs) Child_Add(child);
    }
    
    // =============================================================================
    // Overrides
    // =============================================================================
    
    public override bool ChildLayout_IsFree() { return false; }

    public override TBounds2 ContentBounds()
    {
        return style != null ? style.ContentBounds(bounds) : bounds;
    }

    public override void OnDraw2D(double dt, EDrawFlags flags = 0)
    {
        base.OnDraw2D(dt, flags);
        if (style != null) style.Draw(bounds, new TTransform2());
    }

    protected override void Transform_Refresh()
    {
        _slots_dirty = true;
        base.Transform_Refresh();
    }

    public override TBounds2 Child_MakeBounds2D(ImpComp child, int index)
    {
        if (_slots_dirty) RebuildSlots();
        if ((uint)index >= (uint)_child_slots.Length) return default;
        return _child_slots[index];
    }

    void RebuildSlots()
    {
        _slots_dirty = false;
        int n = children.Count;
        if (_child_slots.Length < n) _child_slots = new TBounds2[n];
        TBounds2 inner = ContentBounds();
        if (box_format == EBoxFormat.Stacked)
        {
            for (int i = 0; i < n; i++)
                _child_slots[i] = children[i] is Imp2D ? inner : default;
            return;
        }

        bool horiz = box_format == EBoxFormat.Horizontal;
        float inner_main = horiz
            ? MathF.Abs(inner.end.X - inner.start.X)
            : MathF.Abs(inner.end.Y - inner.start.Y);

        float preferred_total = 0;
        int fill_n = 0;
        for (int i = 0; i < n; i++)
        {
            if (children[i] is not Imp2D c || !c.is_visible) continue;
            Vector2 sz = PrefSize(c);
            preferred_total += horiz ? sz.X : sz.Y;
            if ((horiz ? c.layout.alignment.align_H : c.layout.alignment.align_V) == ELayoutAlignment.Fill)
                fill_n++;
        }

        float extra = inner_main - preferred_total;
        if (extra < 0) extra = 0;
        float extra_each = fill_n > 0 ? extra / fill_n : 0;

        float leading = 0;
        if (fill_n == 0 && extra > 0)
        {
            ELayoutAlignment? group = null;
            for (int i = 0; i < n; i++)
            {
                if (children[i] is not Imp2D c || !c.is_visible) continue;
                ELayoutAlignment a = horiz ? c.layout.alignment.align_H : c.layout.alignment.align_V;
                if (group == null) group = a;
                else if (group != a) { group = ELayoutAlignment.Start; break; }
            }
            if (group == ELayoutAlignment.Center) leading = extra * 0.5f;
            else if (group == ELayoutAlignment.End) leading = extra;
        }

        float x0 = MathF.Min(inner.start.X, inner.end.X);
        float y0 = MathF.Min(inner.start.Y, inner.end.Y);
        float x1 = MathF.Max(inner.start.X, inner.end.X);
        float y1 = MathF.Max(inner.start.Y, inner.end.Y);
        float cursor = leading;

        for (int i = 0; i < n; i++)
        {
            if (children[i] is not Imp2D c || !c.is_visible)
            {
                _child_slots[i] = default;
                continue;
            }
            Vector2 sz = PrefSize(c);
            float main = horiz ? sz.X : sz.Y;
            bool fill = (horiz ? c.layout.alignment.align_H : c.layout.alignment.align_V) == ELayoutAlignment.Fill;
            if (fill) main += extra_each;
            if (c.layout.size_max != Vector2.Zero)
            {
                float max = horiz ? c.layout.size_max.X : c.layout.size_max.Y;
                if (max > 0 && main > max) main = max;
            }

            _child_slots[i] = horiz
                ? new TBounds2 { start = new Vector2(x0 + cursor, y0), end = new Vector2(x0 + cursor + main, y1) }
                : new TBounds2 { start = new Vector2(x0, y0 + cursor), end = new Vector2(x1, y0 + cursor + main) };
            cursor += main;
        }
    }

    static Vector2 PrefSize(Imp2D c)
    {
        Vector2 size = c.layout.size;
        if (c.layout.size_max != Vector2.Zero)
            size = Vector2.Clamp(size, c.layout.size_min, c.layout.size_max);
        else
            size = Vector2.Max(size, c.layout.size_min);
        return size;
    }
}

// ####################################################################################################################
// Style Asset
// ####################################################################################################################

public class UI_Box : ImpAsset
{
    [ImpVar] public A_Texture? background; 
    [ImpVar] public TMargins background_nineslice;
    [ImpVar] public Color tint=Color.White;
    [ImpVar] public TMargins inner_margins; // margins seperating the children from the edges of the box
    [ImpVar] public TMargins outer_margins; // margins surrounding the box

    public TBounds2 Draw(TBounds2 bounds, TTransform2 offset) //returning bounds is the bounds for the content margins (taking both inner and outer margins into account)
    {
        TBounds2 visual = TBounds2.Inset(bounds, outer_margins);
        float x = visual.start.X;
        float y = visual.start.Y;
        float w = visual.end.X - x;
        float h = visual.end.Y - y;
        if (w > 1e-4f && h > 1e-4f)
        {
            if (background != null)
                background.Draw(visual, offset, EImageLayout.NineSlice, background_nineslice, tint: tint);
            if (background == null || background.texture.Id == 0)
            {
                Vector2 origin = MathF.Abs((float)offset.rotation) > 1e-4f ? offset.position : Vector2.Zero;
                Raylib.DrawRectanglePro(new Rectangle(x, y, w, h), origin, (float)offset.rotation, tint);
            }
        }
        return TBounds2.Inset(visual, inner_margins);
    }

    public TBounds2 ContentBounds(TBounds2 bounds) => TBounds2.Inset(TBounds2.Inset(bounds, outer_margins), inner_margins);
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATICS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private static TMargins def_inner_margins=new(10, 10, 10, 10);
    private static TMargins def_outer_margins=new(2, 2, 2, 2);
    private static TMargins def_nineslice=new(10, 10, 10, 10);
    
    public static UI_Box BLANK = new()
    {
        tint = Color.Blank,
    };
    public static UI_Box LIGHT = new() { 
        background = A_Texture.UI_BOX_LIGHT,
        background_nineslice = def_nineslice,
        inner_margins = def_inner_margins
    };
    public static UI_Box DARK = new() { 
        background = A_Texture.UI_BOX_DARK,
        background_nineslice = def_nineslice,
        inner_margins = def_inner_margins
    };

    public static UI_Box BTN_A_IDLE = new UI_Box() { };
    public static UI_Box BTN_A_HOVER = new UI_Box() { };
    public static UI_Box BTN_A_PRESSED = new UI_Box() { };
    public static UI_Box BTN_A_HIGHLIGHT = new UI_Box() { };
}
