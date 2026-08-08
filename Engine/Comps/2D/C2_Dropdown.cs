using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public struct TDropdownOption
{
    [Export] public string name;

    public TDropdownOption(string name) { this.name = name; }
}

// Closed it draws as a button showing the current option. Open, the list paints through
// ImpUI's overlay layer so it sits above the rest of the tree and swallows the clicks
// underneath it - the same approach C2_MenuBar uses for its menus.
public class C2_Dropdown : ImpComp2D
{
    [ImpVar] public int current_option = -1;
    [ImpVar] public List<TDropdownOption> options = new List<TDropdownOption>();

    //shown when current_option points at nothing (empty list, or a mixed multi-selection)
    public string placeholder_text = "";

    public UIStyle_Rect? style;
    public UIStyle_Text? style_text;

    public Action<C2_Dropdown>? on_dropdown_open;
    public Action<C2_Dropdown>? on_dropdown_close;
    public Action<C2_Dropdown, TDropdownOption, int>? on_dropdown_change;

    bool is_open;

    UIStyle_Text StyleText => style_text ?? Theme_Get().style_text;

    public bool IsOpen => is_open;

    public override Vector2 Size_GetContentMin()
    {
        var th = Theme_Get();

        float w = 0f;
        foreach (var o in options) w = MathF.Max(w, ImpUI.TextMeasure(o.name, StyleText).X);

        return new Vector2(w + th.padding * 4, th.item_height);
    }

    public void Option_SetQuiet(int index)
    {
        current_option = index;
    }

    public void Options_Set(IEnumerable<string> names)
    {
        options.Clear();
        foreach (var n in names) options.Add(new TDropdownOption(n));
    }

    string Label_Get()
    {
        return current_option >= 0 && current_option < options.Count
            ? options[current_option].name
            : placeholder_text;
    }

    // ---------------------------------------------------
    // input
    // ---------------------------------------------------

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        if (ev != ECursorEvent.Clicked) return;
        Open_Set(!is_open);
    }

    void Open_Set(bool open)
    {
        if (open == is_open) return;

        is_open = open;
        if (open) on_dropdown_open?.Invoke(this);
        else on_dropdown_close?.Invoke(this);
    }

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();

        var bg = style?.color
                 ?? (is_open ? th.col_pressed
                     : ImpUI.IsHovered(this) ? th.col_hover
                     : th.col_panel_alt);

        ImpUI.Rect(rect, bg);
        if (is_open) ImpUI.RectOutline(rect, 1f, th.col_accent);

        var tr = new Rectangle(rect.X + th.padding, rect.Y,
                               MathF.Max(0, rect.Width - th.padding * 3), rect.Height);
        ImpUI.TextInRect(Label_Get(), tr, StyleText, 0f);

        Arrow_Draw(th);

        if (is_open) Popup_Update(th);
    }

    void Arrow_Draw(ImpUITheme th)
    {
        float s = 4f;
        float cx = rect.X + rect.Width - th.padding;
        float cy = rect.Y + rect.Height * 0.5f;

        // a small chevron, drawn from two lines so it follows the theme colour
        ImpUI.Line(new Vector2(cx - s, cy - s * 0.5f), new Vector2(cx, cy + s * 0.5f), 1.5f, th.col_text);
        ImpUI.Line(new Vector2(cx, cy + s * 0.5f), new Vector2(cx + s, cy - s * 0.5f), 1.5f, th.col_text);
    }

    void Popup_Update(ImpUITheme th)
    {
        if (options.Count == 0)
        {
            Open_Set(false);
            return;
        }

        float h = options.Count * th.item_height;
        var panel = new Rectangle(rect.X, rect.Y + rect.Height, rect.Width, h);

        // keep the list on screen when the field sits near the bottom edge
        if (panel.Y + panel.Height > ImpUI.screen.Height) panel.Y = MathF.Max(0, rect.Y - panel.Height);

        if (ImpUI.mouse_pressed && !Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, panel))
        {
            // a press on the field itself is handled by Cursor_OnEvent, so only close here
            if (!Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, rect)) Open_Set(false);
            return;
        }

        int clicked = ImpUI.mouse_pressed ? Option_At(th, panel, ImpUI.mouse_pos) : -1;

        var text_style = StyleText;
        ImpUI.Overlay_Push(panel, () => Popup_Draw(th, panel, text_style));

        if (clicked < 0) return;

        Open_Set(false);
        current_option = clicked;
        on_dropdown_change?.Invoke(this, options[clicked], clicked);
    }

    int Option_At(ImpUITheme th, Rectangle panel, Vector2 p)
    {
        if (!Raylib.CheckCollisionPointRec(p, panel)) return -1;

        int i = (int)((p.Y - panel.Y) / th.item_height);
        return i >= 0 && i < options.Count ? i : -1;
    }

    void Popup_Draw(ImpUITheme th, Rectangle panel, UIStyle_Text text_style)
    {
        ImpUI.Rect(panel, th.col_panel_alt);
        ImpUI.RectOutline(panel, 1f, th.col_line);

        for (int i = 0; i < options.Count; i++)
        {
            var ir = new Rectangle(panel.X, panel.Y + i * th.item_height, panel.Width, th.item_height);

            if (i == current_option) ImpUI.Rect(ir, th.col_panel);
            if (Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, ir)) ImpUI.Rect(ir, th.col_accent);

            var tr = new Rectangle(ir.X + th.padding, ir.Y, MathF.Max(0, ir.Width - th.padding * 2), ir.Height);
            ImpUI.TextInRect(options[i].name, tr, text_style, 0f);
        }
    }
}
