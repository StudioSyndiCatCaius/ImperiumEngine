using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// Clips its children and slides them vertically. Put a C2_List inside it for a
// scrolling list.
public class C2_ScrollBox : ImpComp2D
{
    public float scroll = 0f;
    public float scroll_speed = 40f;

    public UIStyle_Rect? style;

    float content_len; //how tall the children want to be, measured during layout

    public C2_ScrollBox()
    {
        clip_contents = true;
    }

    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    protected override void Layout_Children(Rectangle content)
    {
        content_len = 0f;
        foreach (var c in children)
        {
            if (!c.is_visible || c is not ImpComp2D k) continue;
            content_len = MathF.Max(content_len, k.size.Y > 0 ? k.size.Y : k.Size_GetContentMin().Y);
        }

        float overflow = MathF.Max(0f, content_len - content.Height);
        scroll = Math.Clamp(scroll, 0f, overflow);

        // children get the full content height and are shifted up by the scroll offset;
        // clip_contents hides whatever falls outside
        float w = content.Width - (overflow > 0f ? Theme_Get().scrollbar_width : 0f);
        var area = new Rectangle(content.X, content.Y - scroll, w, MathF.Max(content.Height, content_len));

        foreach (var c in children)
        {
            if (!c.is_visible) continue;
            if (c is ImpComp2D k) k.OnLayout_Exact(area);
            else c.OnLayout(area);
        }
    }

    // ---------------------------------------------------
    // input
    // ---------------------------------------------------

    public override bool Cursor_WantsWheel() => true;

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        if (ev == ECursorEvent.Wheel) scroll -= ImpUI.wheel * scroll_speed;
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        if (style != null) ImpUI.Rect(rect, style.color);

        float overflow = MathF.Max(0f, content_len - rect_content.Height);
        if (overflow <= 0f) return;

        var th = Theme_Get();

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
