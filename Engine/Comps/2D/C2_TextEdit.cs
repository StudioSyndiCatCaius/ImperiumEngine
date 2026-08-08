using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

// Single-line text field. Focus is owned by ImpUI - the comp is editable exactly while
// it is ImpUI.focused, which the input pass sets on click.
public class C2_TextEdit : ImpComp2D
{
    [ImpVar] public string text = "";
    [ImpVar] public string placeholder_text = "";
    [ImpVar] public bool is_readonly = false;

    public UIStyle_Rect? style;
    public UIStyle_Text? style_text;

    public Action<string>? on_text_changed;
    public Action<string>? on_submit; //enter pressed, or focus left the field

    int caret;
    double blink;
    bool was_focused;
    float scroll_px; //horizontal offset so the caret stays inside the box on long text

    UIStyle_Text StyleText => style_text ?? Theme_Get().style_text;
    UIStyle_Text StyleHint => Theme_Get().style_text_dim;

    public bool IsFocused => ImpUI.focused == this;

    public override Vector2 Size_GetContentMin()
    {
        var th = Theme_Get();
        return new Vector2(ImpUI.TextMeasure(text, StyleText).X + th.padding * 2, th.item_height);
    }

    // Sets the text without firing on_text_changed - for pushing a value in from outside.
    public void Text_SetQuiet(string value)
    {
        text = value ?? "";
        caret = Math.Clamp(caret, 0, text.Length);
    }

    // ---------------------------------------------------
    // input
    // ---------------------------------------------------

    public override void Cursor_OnEvent(ECursorEvent ev)
    {
        // clicking places the caret at the nearest character boundary
        if (ev == ECursorEvent.Pressed) caret = Caret_At(ImpUI.mouse_pos.X);
    }

    // Walks the string measuring prefixes and takes the boundary closest to x. Fine for
    // single-line fields; a long document would want a cached glyph run instead.
    int Caret_At(float x)
    {
        float origin = rect.X + Theme_Get().padding - scroll_px;

        int best = 0;
        float best_d = MathF.Abs(x - origin);

        for (int i = 1; i <= text.Length; i++)
        {
            float w = ImpUI.TextMeasure(text.Substring(0, i), StyleText).X;
            float d = MathF.Abs(x - (origin + w));
            if (d >= best_d) continue;
            best_d = d;
            best = i;
        }
        return best;
    }

    void Input_Update()
    {
        caret = Math.Clamp(caret, 0, text.Length);
        string before = text;

        // typed characters
        for (int c = Raylib.GetCharPressed(); c != 0; c = Raylib.GetCharPressed())
        {
            if (c < 32) continue;
            text = text.Insert(caret, char.ConvertFromUtf32(c));
            caret++;
        }

        if (Key(KeyboardKey.Backspace) && caret > 0)
        {
            text = text.Remove(caret - 1, 1);
            caret--;
        }
        if (Key(KeyboardKey.Delete) && caret < text.Length) text = text.Remove(caret, 1);

        if (Key(KeyboardKey.Left)) caret = Math.Max(0, caret - 1);
        if (Key(KeyboardKey.Right)) caret = Math.Min(text.Length, caret + 1);
        if (Key(KeyboardKey.Home)) caret = 0;
        if (Key(KeyboardKey.End)) caret = text.Length;

        if (text != before)
        {
            blink = 0;
            on_text_changed?.Invoke(text);
        }

        // Enter commits and drops focus, so the field behaves the same whether the user
        // presses enter or clicks away.
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            ImpUI.focused = null;
            on_submit?.Invoke(text);
        }
    }

    static bool Key(KeyboardKey k) => Raylib.IsKeyPressed(k) || Raylib.IsKeyPressedRepeat(k);

    // ---------------------------------------------------
    // draw
    // ---------------------------------------------------

    protected override void Draw_Self(double dt, EDrawFlags flags)
    {
        var th = Theme_Get();
        bool focus = IsFocused && !is_readonly;

        // committing on focus loss keeps on_submit meaningful for parsed fields (numbers)
        if (was_focused && !focus) on_submit?.Invoke(text);
        was_focused = focus;

        if (focus) Input_Update();

        var bg = style?.color
                 ?? (is_readonly ? th.col_disabled
                     : focus ? th.col_background
                     : ImpUI.IsHovered(this) ? th.col_hover
                     : th.col_panel_alt);

        ImpUI.Rect(rect, bg);
        if (focus) ImpUI.RectOutline(rect, 1f, th.col_accent);

        var inner = new Rectangle(rect.X + th.padding, rect.Y,
                                  MathF.Max(0, rect.Width - th.padding * 2), rect.Height);

        Scroll_Update(inner);

        ImpUI.Clip_Push(inner);

        // The hint stays up while focused and empty, so a field opened for retyping still
        // says what it held. The caret draws over it, which is the usual convention.
        if (text.Length == 0)
        {
            ImpUI.TextInRect(placeholder_text, inner, StyleHint, 0f);
        }
        else
        {
            var m = ImpUI.TextMeasure(text, StyleText);
            ImpUI.Text(text, new Vector2(inner.X - scroll_px, inner.Y + (inner.Height - m.Y) * 0.5f), StyleText);
        }

        if (focus) Caret_Draw(inner, th, dt);

        ImpUI.Clip_Pop();
    }

    void Scroll_Update(Rectangle inner)
    {
        if (!IsFocused)
        {
            scroll_px = 0f;
            return;
        }

        float cx = ImpUI.TextMeasure(text.Substring(0, Math.Clamp(caret, 0, text.Length)), StyleText).X;

        if (cx - scroll_px > inner.Width) scroll_px = cx - inner.Width;
        if (cx - scroll_px < 0) scroll_px = cx;

        float total = ImpUI.TextMeasure(text, StyleText).X;
        scroll_px = Math.Clamp(scroll_px, 0f, MathF.Max(0f, total - inner.Width));
    }

    void Caret_Draw(Rectangle inner, ImpUITheme th, double dt)
    {
        blink += dt;
        if (blink % 1.0 > 0.5) return;

        float cx = inner.X - scroll_px
                   + ImpUI.TextMeasure(text.Substring(0, Math.Clamp(caret, 0, text.Length)), StyleText).X;

        float h = StyleText.size;
        ImpUI.Rect(new Rectangle(cx, inner.Y + (inner.Height - h) * 0.5f, 1f, h), th.col_text);
    }
}
