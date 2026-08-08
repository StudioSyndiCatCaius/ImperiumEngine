using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// A colour swatch that opens a picker panel: saturation/value square, hue strip, and an
// alpha strip.
//
// Hue, saturation and value are held here as the real state rather than being read back
// out of the colour each frame. RGB carries no hue at all once saturation or value reaches
// zero, so a round trip through it would snap the cursor to red the instant the user
// dragged into black or white.
//
// The panel paints through ImpUI's overlay layer so it escapes the inspector's scroll clip
// and sits above the rest of the tree - the same approach C2_Dropdown uses.
public class C2_ColorPicker : ImpComp2D
{
    // panel metrics
    const float PANEL_WIDTH = 224f;
    const float SV_HEIGHT = 128f;
    const float BAR_HEIGHT = 14f;
    const float PANEL_PAD = 6f;
    const float CHECKER_CELL = 5f;

    //which strip a drag started in; strips keep the pointer until release
    const int AREA_NONE = -1;
    const int AREA_SV = 0;
    const int AREA_HUE = 1;
    const int AREA_ALPHA = 2;

    [ImpVar] public Color color = Color.White;
    [ImpVar] public bool show_alpha = true;

    //multi-selection disagreement; the swatch shows nothing rather than a lie
    public bool is_mixed;

    public UIStyle_Rect? style;

    public Action<C2_ColorPicker>? on_color_changed;

    bool is_open;
    int drag_area = AREA_NONE;

    float hue;   //0-360
    float sat;   //0-1
    float val;   //0-1
    float alpha = 1f;

    public bool IsOpen => is_open;

    public C2_ColorPicker()
    {
        cursor_filter = ECursorFilter.Hit;
        HSV_Sync();
    }

    public override Vector2 Size_GetContentMin() => new Vector2(0, Theme_Get().item_height);

    // ---------------------------------------------------
    // value
    // ---------------------------------------------------

    public void Color_Set(Color value, bool notify = true)
    {
        color = value;
        HSV_Sync();

        if (notify) on_color_changed?.Invoke(this);
    }

    // Pushing a value in from outside. The early out matters while the panel is open: the
    // inspector writes back the colour this picker just produced, and re-deriving HSV from
    // it would undo the hue the user is holding.
    public void Color_SetQuiet(Color value)
    {
        if (value.R == color.R && value.G == color.G && value.B == color.B && value.A == color.A) return;

        color = value;
        HSV_Sync();
    }

    void HSV_Sync()
    {
        Vector3 hsv = Raylib.ColorToHSV(color);

        //hue is undefined on greys, so hold onto whatever is already showing
        if (hsv.Y > 0f && hsv.Z > 0f) hue = hsv.X;

        sat = hsv.Y;
        val = hsv.Z;
        alpha = color.A / 255f;
    }

    void HSV_Apply()
    {
        Color rgb = Raylib.ColorFromHSV(hue, sat, val);

        color = new Color(rgb.R, rgb.G, rgb.B, (byte)Math.Clamp(alpha * 255f, 0f, 255f));
        is_mixed = false;

        on_color_changed?.Invoke(this);
    }

    Color Color_Opaque() => new Color(color.R, color.G, color.B, (byte)255);

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
        drag_area = AREA_NONE;

        //re-derive on open so the panel starts from whatever the value is now
        if (open) HSV_Sync();
    }

    // ---------------------------------------------------
    // panel
    // ---------------------------------------------------

    struct TPanel
    {
        public Rectangle panel;
        public Rectangle sv;
        public Rectangle hue;
        public Rectangle alpha;
        public Rectangle preview;
    }

    TPanel Panel_Layout(ImpUITheme th)
    {
        var p = new TPanel();

        float w = MathF.Max(PANEL_WIDTH, rect.Width);
        float inner = w - PANEL_PAD * 2;

        float h = PANEL_PAD
                + SV_HEIGHT + PANEL_PAD
                + BAR_HEIGHT + PANEL_PAD
                + (show_alpha ? BAR_HEIGHT + PANEL_PAD : 0f)
                + th.item_height + PANEL_PAD;

        float x = rect.X;
        float y = rect.Y + rect.Height;

        //keep the panel on screen when the row sits near an edge
        if (y + h > ImpUI.screen.Height) y = MathF.Max(0, rect.Y - h);
        if (x + w > ImpUI.screen.Width) x = MathF.Max(0, ImpUI.screen.Width - w);

        p.panel = new Rectangle(x, y, w, h);

        float cy = y + PANEL_PAD;
        p.sv = new Rectangle(x + PANEL_PAD, cy, inner, SV_HEIGHT);
        cy += SV_HEIGHT + PANEL_PAD;

        p.hue = new Rectangle(x + PANEL_PAD, cy, inner, BAR_HEIGHT);
        cy += BAR_HEIGHT + PANEL_PAD;

        if (show_alpha)
        {
            p.alpha = new Rectangle(x + PANEL_PAD, cy, inner, BAR_HEIGHT);
            cy += BAR_HEIGHT + PANEL_PAD;
        }

        p.preview = new Rectangle(x + PANEL_PAD, cy, inner, th.item_height);
        return p;
    }

    void Panel_Update(ImpUITheme th)
    {
        var p = Panel_Layout(th);

        if (ImpUI.mouse_pressed) drag_area = Area_At(p, ImpUI.mouse_pos);
        if (!ImpUI.mouse_down) drag_area = AREA_NONE;

        if (drag_area != AREA_NONE)
        {
            // the press owns the pointer until release, so a fast drag that runs off the
            // edge of a strip keeps picking instead of stopping dead at the boundary
            Drag_Apply(p, ImpUI.mouse_pos);
        }
        else if (ImpUI.mouse_pressed
                 && !Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, p.panel)
                 && !Raylib.CheckCollisionPointRec(ImpUI.mouse_pos, rect))
        {
            //a press on the swatch itself is Cursor_OnEvent's business, so only close here
            Open_Set(false);
            return;
        }

        ImpUI.Overlay_Push(p.panel, () => Panel_Draw(th, p));
    }

    int Area_At(TPanel p, Vector2 point)
    {
        if (Raylib.CheckCollisionPointRec(point, p.sv)) return AREA_SV;
        if (Raylib.CheckCollisionPointRec(point, p.hue)) return AREA_HUE;
        if (show_alpha && Raylib.CheckCollisionPointRec(point, p.alpha)) return AREA_ALPHA;
        return AREA_NONE;
    }

    void Drag_Apply(TPanel p, Vector2 point)
    {
        switch (drag_area)
        {
            case AREA_SV:
                sat = Fraction(point.X, p.sv.X, p.sv.Width);
                val = 1f - Fraction(point.Y, p.sv.Y, p.sv.Height);
                break;

            case AREA_HUE:
                hue = Fraction(point.X, p.hue.X, p.hue.Width) * 360f;
                break;

            case AREA_ALPHA:
                alpha = Fraction(point.X, p.alpha.X, p.alpha.Width);
                break;

            default: return;
        }

        HSV_Apply();
    }

    static float Fraction(float v, float start, float length)
    {
        return length <= 0f ? 0f : Math.Clamp((v - start) / length, 0f, 1f);
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

        //the swatch is inset so the row's background still reads as the field
        var swatch = new Rectangle(rect.X + 2f, rect.Y + 2f,
                                   MathF.Max(0, rect.Width - 4f), MathF.Max(0, rect.Height - 4f));

        if (is_mixed)
        {
            ImpUI.TextInRect("-", rect, th.style_text_dim, 0.5f);
        }
        else
        {
            Swatch_Draw(swatch, th);
        }

        ImpUI.RectOutline(rect, 1f, is_open ? th.col_accent : th.col_line);

        if (is_open) Panel_Update(th);
    }

    void Swatch_Draw(Rectangle r, ImpUITheme th)
    {
        if (color.A < 255) ImpUI.RectChecker(r, th.col_panel, th.col_panel_alt, CHECKER_CELL);
        ImpUI.Rect(r, color);
    }

    void Panel_Draw(ImpUITheme th, TPanel p)
    {
        ImpUI.Rect(p.panel, th.col_panel_alt);
        ImpUI.RectOutline(p.panel, 1f, th.col_line);

        SV_Draw(p.sv, th);
        Hue_Draw(p.hue);
        if (show_alpha) Alpha_Draw(p.alpha, th);

        Preview_Draw(p.preview, th);
    }

    // White to full hue across, then a black fade down. Two blended passes rather than a
    // per-pixel fill: the gradient primitive does it on the GPU for free.
    void SV_Draw(Rectangle r, ImpUITheme th)
    {
        Color pure = Raylib.ColorFromHSV(hue, 1f, 1f);
        var clear = new Color(0, 0, 0, 0);

        ImpUI.RectGradient(r, Color.White, Color.White, pure, pure);
        ImpUI.RectGradient(r, clear, Color.Black, Color.Black, clear);
        ImpUI.RectOutline(r, 1f, th.col_line);

        Cursor_Draw(new Vector2(r.X + sat * r.Width, r.Y + (1f - val) * r.Height));
    }

    // A ring, drawn as two boxes: the outline primitive is all there is, and a light box
    // inside a dark one stays visible over both ends of the square.
    static void Cursor_Draw(Vector2 at)
    {
        const float s = 9f;
        var box = new Rectangle(at.X - s * 0.5f, at.Y - s * 0.5f, s, s);

        ImpUI.RectOutline(box, 2f, Color.Black);
        ImpUI.RectOutline(new Rectangle(box.X + 1f, box.Y + 1f, box.Width - 2f, box.Height - 2f),
                          1f, Color.White);
    }

    // Six gradient segments, because a hue sweep is six linear ramps and not one.
    void Hue_Draw(Rectangle r)
    {
        const int steps = 6;
        float w = r.Width / steps;

        for (int i = 0; i < steps; i++)
        {
            Color a = Raylib.ColorFromHSV(i * 60f, 1f, 1f);
            Color b = Raylib.ColorFromHSV((i + 1) * 60f, 1f, 1f);

            ImpUI.RectGradient(new Rectangle(r.X + i * w, r.Y, w, r.Height), a, a, b, b);
        }

        Marker_Draw(r, hue / 360f);
    }

    void Alpha_Draw(Rectangle r, ImpUITheme th)
    {
        ImpUI.RectChecker(r, th.col_panel, th.col_background, CHECKER_CELL);

        Color opaque = Color_Opaque();
        var clear = new Color(opaque.R, opaque.G, opaque.B, (byte)0);

        ImpUI.RectGradient(r, clear, clear, opaque, opaque);
        Marker_Draw(r, alpha);
    }

    static void Marker_Draw(Rectangle bar, float t)
    {
        float x = bar.X + Math.Clamp(t, 0f, 1f) * bar.Width;

        var box = new Rectangle(x - 2f, bar.Y - 2f, 4f, bar.Height + 4f);
        ImpUI.RectOutline(box, 2f, Color.Black);
        ImpUI.RectOutline(new Rectangle(box.X + 1f, box.Y + 1f, box.Width - 2f, box.Height - 2f),
                          1f, Color.White);
    }

    void Preview_Draw(Rectangle r, ImpUITheme th)
    {
        float sw = r.Height * 1.6f;
        var swatch = new Rectangle(r.X, r.Y, sw, r.Height);

        if (color.A < 255) ImpUI.RectChecker(swatch, th.col_panel, th.col_background, CHECKER_CELL);
        ImpUI.Rect(swatch, color);
        ImpUI.RectOutline(swatch, 1f, th.col_line);

        var text = new Rectangle(r.X + sw + PANEL_PAD, r.Y,
                                 MathF.Max(0, r.Width - sw - PANEL_PAD), r.Height);

        ImpUI.TextInRect(Hex_Get(), text, th.style_text, 0f);
    }

    string Hex_Get()
    {
        return show_alpha
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
