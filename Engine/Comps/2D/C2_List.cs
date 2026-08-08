using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// Stacks its children along one axis. For a scrolling list, nest this inside a
// C2_ScrollBox rather than making the list scroll itself.
public class C2_List : ImpComp2D
{
    [ImpVar] public EUIAlignment Alignment = EUIAlignment.Vertical;
    [ImpVar] public float separation = 4f;

    //if true, ignores designated child size and stretches children to in-total fill this
    [ImpVar] public bool stretch_children;

    [ImpVar] public UIStyle_Rect? style;
    
    bool Vertical => Alignment == EUIAlignment.Vertical;
    
    // ---------------------------------------------------
    // Options
    // ---------------------------------------------------

    /*
     *  Options are a way a list can handle & popular its children with selection and other bindings
     */
    
    public Action<C2_List, ImpComp2D, int> on_option_selected;
    public Action<C2_List, ImpComp2D, int, bool> on_option_hovered;
    
    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    protected override void Layout_Children(Rectangle content)
    {
        var kids = Kids_Get();
        if (kids.Count == 0) return;

        float avail = (Vertical ? content.Height : content.Width) - separation * (kids.Count - 1);

        // pass 1: how much does each child want along the main axis
        var len = new float[kids.Count];
        float used = 0f;
        float expand_total = 0f;

        for (int i = 0; i < kids.Count; i++)
        {
            len[i] = Main_GetWant(kids[i]);
            used += len[i];
            if (Kid_Expands(kids[i])) expand_total += kids[i].expand_ratio;
        }

        // pass 2: hand the leftover space to expanding children, split by ratio
        float leftover = avail - used;
        if (leftover > 0f && expand_total > 0f)
        {
            for (int i = 0; i < kids.Count; i++)
            {
                if (Kid_Expands(kids[i])) len[i] += leftover * (kids[i].expand_ratio / expand_total);
            }
        }

        // pass 3: place each child in its slot
        float cross_avail = Vertical ? content.Width : content.Height;
        float pos = Vertical ? content.Y : content.X;

        for (int i = 0; i < kids.Count; i++)
        {
            var k = kids[i];
            Cross_Resolve(k, cross_avail, out float cross_len, out float cross_off);

            var slot = Vertical
                ? new Rectangle(content.X + cross_off, pos, cross_len, len[i])
                : new Rectangle(pos, content.Y + cross_off, len[i], cross_len);

            k.OnLayout_Exact(slot);
            pos += len[i] + separation;
        }
    }

    public override Vector2 Size_GetContentMin()
    {
        var kids = Kids_Get();
        if (kids.Count == 0) return Vector2.Zero;

        float main = separation * (kids.Count - 1);
        float cross = 0f;

        foreach (var k in kids)
        {
            main += Main_GetWant(k);
            cross = MathF.Max(cross, Cross_GetWant(k));
        }

        return Vertical ? new Vector2(cross, main) : new Vector2(main, cross);
    }

    // ---------------------------------------------------
    // helpers
    // ---------------------------------------------------

    List<ImpComp2D> Kids_Get()
    {
        var kids = new List<ImpComp2D>();
        foreach (var c in children)
        {
            if (c.is_visible && c is ImpComp2D k) kids.Add(k);
        }
        return kids;
    }

    bool Kid_Expands(ImpComp2D k)
    {
        return stretch_children || (Vertical ? k.sizing_vertical.expand : k.sizing_horizontal.expand);
    }

    float Main_GetWant(ImpComp2D k)
    {
        var min = k.Size_GetContentMin();
        return Vertical
            ? (k.size.Y > 0 ? k.size.Y : min.Y)
            : (k.size.X > 0 ? k.size.X : min.X);
    }

    float Cross_GetWant(ImpComp2D k)
    {
        var min = k.Size_GetContentMin();
        return Vertical
            ? (k.size.X > 0 ? k.size.X : min.X)
            : (k.size.Y > 0 ? k.size.Y : min.Y);
    }

    // Fill takes the whole cross axis; the Shrink presets take what they want and sit at
    // the start, centre or end of it.
    void Cross_Resolve(ImpComp2D k, float cross_avail, out float cross_len, out float cross_off)
    {
        var sizing = Vertical ? k.sizing_horizontal : k.sizing_vertical;

        if (stretch_children || sizing.preset == EUISizingPreset.Fill)
        {
            cross_len = cross_avail;
            cross_off = 0f;
            return;
        }

        cross_len = MathF.Min(Cross_GetWant(k), cross_avail);
        cross_off = sizing.preset switch
        {
            EUISizingPreset.ShrinkCenter => (cross_avail - cross_len) * 0.5f,
            EUISizingPreset.ShrinkEnd    => cross_avail - cross_len,
            _                            => 0f,
        };
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        if (style != null) ImpUI.Rect(rect, style.color);
    }
}
