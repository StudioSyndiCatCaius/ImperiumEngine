using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

//equivalent of TabContainer in Godot
public class C2_TabBox : ImpComp2D
{
    public int active_tab = 0;

    [ImpVar] public UIStyle_Rect? style;
    [ImpVar] public UIStyle_Text? style_text;
    [ImpVar] public bool can_close_tabs;
    
    public Action<C2_TabBox, int>? on_tab_changed;
    public Action<C2_TabBox, int, ImpComp>? on_request_close_tab;

    readonly List<Rectangle> tab_rects = new List<Rectangle>();
    Rectangle rect_tabs;
    Rectangle rect_body;

    UIStyle_Rect Style => style ?? Theme_Get().style_rect;
    UIStyle_Text StyleText => style_text ?? Theme_Get().style_text;

    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    protected override void Layout_Children(Rectangle content)
    {
        var th = Theme_Get();

        rect_tabs = new Rectangle(content.X, content.Y, content.Width, th.tab_height);
        rect_body = new Rectangle(
            content.X, content.Y + th.tab_height,
            content.Width, MathF.Max(0, content.Height - th.tab_height));

        tab_rects.Clear();
        float x = rect_tabs.X;
        foreach (var c in children)
        {
            float w = ImpUI.TextMeasure(Tab_Name(c), StyleText).X + th.padding * 3;
            tab_rects.Add(new Rectangle(x, rect_tabs.Y, w, rect_tabs.Height));
            x += w;
        }

        if (children.Count > 0) active_tab = Math.Clamp(active_tab, 0, children.Count - 1);

        // Only the active tab is laid out. Everything else has its geometry wiped, so no
        // inactive tab can be drawn or hit-tested off a stale rect.
        for (int i = 0; i < children.Count; i++)
        {
            if (i == active_tab && children[i].is_visible) children[i].OnLayout(rect_body);
            else Rect_Clear(children[i]);
        }
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        ImpUI.Rect(rect_body, Style.color);
        ImpUI.Rect(rect_tabs, th.col_panel_alt);

        Input_Update();

        for (int i = 0; i < children.Count && i < tab_rects.Count; i++)
        {
            var r = tab_rects[i];
            bool is_active = i == active_tab;

            if (is_active)
            {
                ImpUI.Rect(r, Style.color);
                ImpUI.Rect(new Rectangle(r.X, r.Y, r.Width, 2), th.col_accent);
            }
            else if (ImpUI.IsHovered(this) && Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, r))
            {
                ImpUI.Rect(r, th.col_hover);
            }

            ImpUI.TextInRect(Tab_Name(children[i]), r, StyleText, 0.5f);
        }
    }

    protected override void Draw_Children(double dt, EDrawFlags flags)
    {
        var active = Child_GetActive();
        if (active != null && active.is_visible) active.OnDraw(dt, flags);
    }

    void Input_Update()
    {
        if (!ImpUI.mouse_pressed || !ImpUI.IsHovered(this)) return;

        for (int i = 0; i < tab_rects.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, tab_rects[i]))
            {
                active_tab = i;
                return;
            }
        }
    }

    public ImpComp? Child_GetActive()
    {
        return active_tab >= 0 && active_tab < children.Count ? children[active_tab] : null;
    }

    static string Tab_Name(ImpComp c) => string.IsNullOrEmpty(c.name) ? "Tab" : c.name;
}
