using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public struct TMenuBarOption
{
    [Export] public string name;
    public TMenuBarSubption[] subptions;

    public TMenuBarOption(string name, TMenuBarSubption[] subptions)
    {
        this.name = name;
        this.subptions = subptions;
    }
}

public struct TMenuBarSubption
{
    [Export] public string name="";
    public bool is_seperator=false;

    public Action? Do;

    public TMenuBarSubption(string? name, Action? do_action)
    {
        this.is_seperator = false;
        this.name = name ?? "";
        Do = do_action;
    }

    public TMenuBarSubption(bool is_seperator)
    {
        this.is_seperator = is_seperator;
        Do = null;
    }
}

public class C2_MenuBar : ImpComp2D
{
    [Export] public TMenuBarOption[] options;

    public UIStyle_Rect? style;
    public UIStyle_Text? style_text;

    public int open_index = -1; //-1 = closed; settable to open a menu programmatically
    readonly List<Rectangle> option_rects = new List<Rectangle>();

    UIStyle_Rect Style => style ?? Theme_Get().style_rect;
    UIStyle_Text StyleText => style_text ?? Theme_Get().style_text;

    public C2_MenuBar(TMenuBarOption[] options)
    {
        this.options = options;

        anchor_preset = EUIAnchorPreset.WideTop;
    }

    // Height comes from the theme rather than a fixed size, so it follows a theme swap.
    public override Vector2 Size_GetContentMin()
    {
        return new Vector2(0, Theme_Get().menubar_height);
    }

    // ---------------------------------------------------
    // layout
    // ---------------------------------------------------

    protected override void Layout_Children(Rectangle content)
    {
        var th = Theme_Get();
        option_rects.Clear();

        float x = content.X + th.padding;
        foreach (var opt in options)
        {
            float w = ImpUI.TextMeasure(opt.name, StyleText).X + th.padding * 2;
            option_rects.Add(new Rectangle(x, content.Y, w, content.Height));
            x += w;
        }

        base.Layout_Children(content);
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        ImpUI.Rect(rect, Style.color);

        Input_Update();

        for (int i = 0; i < options.Length && i < option_rects.Count; i++)
        {
            var r = option_rects[i];

            if (i == open_index) ImpUI.Rect(r, th.col_accent);
            else if (Option_IsHot(r)) ImpUI.Rect(r, th.col_hover);

            ImpUI.TextInRect(options[i].name, r, StyleText, 0.5f);
        }

        if (open_index >= 0 && open_index < options.Length) Dropdown_Update(open_index);
    }

    bool Option_IsHot(Rectangle r)
    {
        return ImpUI.IsHovered(this) && Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, r);
    }

    int Option_At(Vector2 p)
    {
        for (int i = 0; i < option_rects.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(p, option_rects[i])) return i;
        }
        return -1;
    }

    void Input_Update()
    {
        if (!ImpUI.IsHovered(this)) return;

        int over = Option_At(ImpUI.mouse_pos);

        if (ImpUI.mouse_pressed)
        {
            open_index = over == open_index ? -1 : over;
        }
        else if (open_index >= 0 && over >= 0)
        {
            // with a menu already open, sliding along the bar switches menus
            open_index = over;
        }
    }

    // ---------------------------------------------------
    // dropdown
    // ---------------------------------------------------

    // The dropdown paints through the overlay list so it sits above everything, and it
    // handles its own clicks: ImpUI blocks the tree behind an overlay, so nothing else
    // will react to a press inside the panel.
    void Dropdown_Update(int index)
    {
        var th = Theme_Get();
        var subs = options[index].subptions ?? Array.Empty<TMenuBarSubption>();
        var anchor = option_rects[index];

        float w = th.menu_min_width;
        float h = th.padding * 2;
        foreach (var s in subs)
        {
            if (!s.is_seperator) w = MathF.Max(w, ImpUI.TextMeasure(s.name, StyleText).X + th.padding * 5);
            h += Sub_Height(th, s);
        }

        var panel = new Rectangle(anchor.X, anchor.Y + anchor.Height, w, h);

        if (ImpUI.mouse_pressed && !Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, panel))
        {
            // a press on the bar itself is handled by Input_Update, so only close here
            if (!Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, rect)) open_index = -1;
            return;
        }

        int clicked = ImpUI.mouse_pressed ? Sub_At(th, panel, subs, ImpUI.mouse_pos) : -1;

        var text_style = StyleText;
        ImpUI.Overlay_Push(panel, () => Dropdown_Draw(th, panel, subs, text_style));

        if (clicked >= 0)
        {
            open_index = -1;
            subs[clicked].Do?.Invoke();
        }
    }

    static int Sub_At(ImpUITheme th, Rectangle panel, TMenuBarSubption[] subs, Vector2 p)
    {
        float y = panel.Y + th.padding;
        for (int i = 0; i < subs.Length; i++)
        {
            float ih = Sub_Height(th, subs[i]);
            if (!subs[i].is_seperator
                && Raylib.CheckCollisionPointRec(p, new Rectangle(panel.X, y, panel.Width, ih)))
            {
                return i;
            }
            y += ih;
        }
        return -1;
    }

    static void Dropdown_Draw(ImpUITheme th, Rectangle panel, TMenuBarSubption[] subs, UIStyle_Text text_style)
    {
        ImpUI.Rect(panel, th.col_panel_alt);
        ImpUI.RectOutline(panel, 1f, th.col_line);

        float y = panel.Y + th.padding;
        foreach (var s in subs)
        {
            float ih = Sub_Height(th, s);
            var ir = new Rectangle(panel.X, y, panel.Width, ih);

            if (s.is_seperator)
            {
                float my = ir.Y + ih * 0.5f;
                ImpUI.Line(
                    new Vector2(ir.X + th.padding, my),
                    new Vector2(ir.X + ir.Width - th.padding, my),
                    1f, th.col_line);
            }
            else
            {
                if (Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, ir)) ImpUI.Rect(ir, th.col_accent);

                var tr = new Rectangle(ir.X + th.padding * 2, ir.Y,
                                       ir.Width - th.padding * 3, ir.Height);
                ImpUI.TextInRect(s.name, tr, text_style, 0f);
            }

            y += ih;
        }
    }

    static float Sub_Height(ImpUITheme th, TMenuBarSubption s)
    {
        return s.is_seperator ? th.padding * 1.5f : th.item_height;
    }
}
