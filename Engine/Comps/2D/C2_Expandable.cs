using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// A titled header with an arrow; clicking it expands or collapses the children stacked
// underneath. Collapsed children are neither laid out nor drawn, and their rects are
// wiped so they can't be hit-tested off stale geometry.
public class C2_Expandable : ImpComp2D
{
    [ImpVar] public UIStyle_Expandable? style;
    [ImpVar] public bool is_expanded = true;

    public string title = "";
    public float separation = 2f;
    public float indent = 12f; //how far children are inset from the header

    public UIStyle_Text? style_text;

    public Action<C2_Expandable>? on_expanded_changed;

    Rectangle rect_header;

    UIStyle_Text StyleText => style_text ?? Theme_Get().style_text;

    float Header_Height() => Theme_Get().item_height;

    public void Expanded_Set(bool expanded)
    {
        if (expanded == is_expanded) return;

        is_expanded = expanded;
        on_expanded_changed?.Invoke(this);
    }

    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    public override Vector2 Size_GetContentMin()
    {
        float h = Header_Height();
        float w = ImpUI.TextMeasure(title, StyleText).X + Theme_Get().padding * 3;

        if (!is_expanded) return new Vector2(w, h);

        foreach (var c in children)
        {
            if (!c.is_visible || c is not ImpComp2D k) continue;

            var min = k.Size_GetContentMin();
            h += (k.size.Y > 0 ? k.size.Y : min.Y) + separation;
            w = MathF.Max(w, (k.size.X > 0 ? k.size.X : min.X) + indent);
        }

        return new Vector2(w, h);
    }

    protected override void Layout_Children(Rectangle content)
    {
        rect_header = new Rectangle(content.X, content.Y, content.Width, Header_Height());

        if (!is_expanded)
        {
            foreach (var c in children) { Rect_Clear(c); }
            return;
        }

        float y = rect_header.Y + rect_header.Height + separation;
        float x = content.X + indent;
        float w = MathF.Max(0, content.Width - indent);

        foreach (var c in children)
        {
            if (!c.is_visible)
            {
                Rect_Clear(c);
                continue;
            }

            if (c is not ImpComp2D k)
            {
                c.OnLayout(new Rectangle(x, y, w, content.Y + content.Height - y));
                continue;
            }

            var min = k.Size_GetContentMin();
            float h = k.size.Y > 0 ? k.size.Y : min.Y;

            k.OnLayout_Exact(new Rectangle(x, y, w, h));
            y += h + separation;
        }
    }

    // ---------------------------------------------------
    // input
    // ---------------------------------------------------

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        // only the header toggles: a click in the body area belongs to whatever sits there
        if (ev != ECursorEvent.Clicked) return;
        if (!Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, rect_header)) return;

        Expanded_Set(!is_expanded);
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        bool hot = ImpUI.IsHovered(this) && Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, rect_header);
        var bg = style?.box_style?.color ?? (hot ? th.col_hover : th.col_panel_alt);

        ImpUI.Rect(rect_header, bg);

        Arrow_Draw(th);

        var tr = new Rectangle(rect_header.X + th.padding * 2, rect_header.Y,
                               MathF.Max(0, rect_header.Width - th.padding * 2), rect_header.Height);
        ImpUI.TextInRect(title, tr, StyleText, 0f);
    }

    void Arrow_Draw(ImpUITheme th)
    {
        var tex = is_expanded ? style?.arrow_expanded : style?.arrow_closed;

        float s = 8f;
        var box = new Rectangle(
            rect_header.X + th.padding * 0.5f,
            rect_header.Y + (rect_header.Height - s) * 0.5f,
            s, s);

        if (tex != null)
        {
            ImpUI.Texture(tex, box, th.col_text);
            return;
        }

        // no texture in the style: fall back to a chevron so the state is still readable
        float cx = box.X + s * 0.5f;
        float cy = box.Y + s * 0.5f;

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

    protected override void Draw_Children(double dt, EDrawFlags flags)
    {
        if (!is_expanded) return;
        base.Draw_Children(dt, flags);
    }
}

public class UIStyle_Expandable : ImpAsset
{
    [ImpVar] public UIStyle_Rect? box_style;
    [ImpVar] public A_Texture? arrow_closed;
    [ImpVar] public A_Texture? arrow_expanded;
}
