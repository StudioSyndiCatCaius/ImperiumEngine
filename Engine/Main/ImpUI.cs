using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Main;

// Native UI service. Every raylib call the UI makes goes through here, so clipping,
// colour modulation and overlay ordering all stay consistent in one place.
//
// A UI frame runs as three ordered passes, driven by ImpApp:
//   Frame_Begin -> Layout -> Input_Process -> Draw -> Frame_End

public static class ImpUI
{
    // ----------------------------------------------------------------
    // Frame state
    // ----------------------------------------------------------------
    public static Rectangle screen;
    public static double delta;

    // Input ----------------------------------------------------------
    public static Vector2 mouse_pos;
    public static Vector2 mouse_delta;
    public static bool mouse_pressed;
    public static bool mouse_down;
    public static bool mouse_released;
    public static float wheel;

    public static ImpComp2D? hovered; //comp under the cursor this frame
    public static ImpComp2D? pressed; //comp the mouse button went down on
    public static ImpComp2D? focused; //last comp clicked

    // Clipping -------------------------------------------------------
    static readonly List<Rectangle> clip_stack = new List<Rectangle>();

    // Colour modulation ----------------------------------------------
    static readonly List<Color> modulate_stack = new List<Color>();

    // Overlays -------------------------------------------------------
    // Drawn after the main pass so popups/dropdowns paint above the tree without
    // having to reorder it. Blocker rects are kept a frame so input can be gated.
    static readonly List<Action> overlays = new List<Action>();
    static readonly List<Rectangle> overlay_blockers = new List<Rectangle>();
    static readonly List<Rectangle> overlay_blockers_prev = new List<Rectangle>();

    // ----------------------------------------------------------------
    // Frame
    // ----------------------------------------------------------------

    public static void Frame_Begin(double dt)
    {
        delta = dt;
        screen = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

        mouse_pos = Raylib.GetMousePosition();
        mouse_delta = Raylib.GetMouseDelta();
        mouse_pressed = Raylib.IsMouseButtonPressed(MouseButton.Left);
        mouse_down = Raylib.IsMouseButtonDown(MouseButton.Left);
        mouse_released = Raylib.IsMouseButtonReleased(MouseButton.Left);
        wheel = Raylib.GetMouseWheelMove();

        clip_stack.Clear();
        modulate_stack.Clear();

        overlay_blockers_prev.Clear();
        overlay_blockers_prev.AddRange(overlay_blockers);
        overlay_blockers.Clear();
        overlays.Clear();
    }

    public static void Frame_End()
    {
        // Overlays may themselves push overlays, so drain by index.
        for (int i = 0; i < overlays.Count; i++) { overlays[i]?.Invoke(); }
        overlays.Clear();

        clip_stack.Clear();
        modulate_stack.Clear();
        Raylib.EndScissorMode();
    }

    // ----------------------------------------------------------------
    // Overlays
    // ----------------------------------------------------------------

    // blocker is the screen area the overlay occupies; the cursor over it will not
    // reach the regular tree next frame.
    public static void Overlay_Push(Rectangle blocker, Action draw)
    {
        overlays.Add(draw);
        overlay_blockers.Add(blocker);
    }

    public static bool Overlay_IsBlocking(Vector2 p)
    {
        foreach (var r in overlay_blockers_prev)
        {
            if (Raylib.CheckCollisionPointRec(p, r)) return true;
        }
        return false;
    }

    // ----------------------------------------------------------------
    // Clipping
    // ----------------------------------------------------------------

    public static Rectangle Clip_Current => clip_stack.Count > 0 ? clip_stack[^1] : screen;

    public static void Clip_Push(Rectangle r)
    {
        // intersect with the active clip so nested clip_contents nests rather than replaces
        var clipped = Rect_Intersect(r, Clip_Current);
        clip_stack.Add(clipped);
        Clip_Apply(clipped);
    }

    public static void Clip_Pop()
    {
        if (clip_stack.Count == 0) return;
        clip_stack.RemoveAt(clip_stack.Count - 1);

        if (clip_stack.Count == 0) Raylib.EndScissorMode();
        else Clip_Apply(clip_stack[^1]);
    }

    static void Clip_Apply(Rectangle r)
    {
        Raylib.BeginScissorMode((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
    }

    // Suspends and restores the scissor around code that drives GL itself - the 3D render
    // sessions a scene viewport opens. The clip stack is left alone, so whatever was
    // clipping before is clipping again afterwards.
    public static void Clip_Suspend() { Raylib.EndScissorMode(); }

    public static void Clip_Resume()
    {
        if (clip_stack.Count > 0) Clip_Apply(clip_stack[^1]);
    }

    // ----------------------------------------------------------------
    // Colour modulation
    // ----------------------------------------------------------------

    public static Color Modulate_Current => modulate_stack.Count > 0 ? modulate_stack[^1] : Color.White;

    public static void Modulate_Push(Color c)
    {
        modulate_stack.Add(Color_Mul(c, Modulate_Current));
    }

    public static void Modulate_Pop()
    {
        if (modulate_stack.Count > 0) modulate_stack.RemoveAt(modulate_stack.Count - 1);
    }

    // ----------------------------------------------------------------
    // Draw primitives
    // ----------------------------------------------------------------

    public static void Rect(Rectangle r, Color c)
    {
        Raylib.DrawRectangleRec(r, Color_Mul(c, Modulate_Current));
    }

    public static void RectOutline(Rectangle r, float thickness, Color c)
    {
        Raylib.DrawRectangleLinesEx(r, thickness, Color_Mul(c, Modulate_Current));
    }

    // Four-corner gradient. The only primitive here that isn't a flat fill: a colour picker
    // can't be drawn without one, and nothing else needs it, so it stays a thin wrapper
    // rather than growing a UIStyle of its own.
    public static void RectGradient(Rectangle r, Color top_left, Color bottom_left, Color bottom_right, Color top_right)
    {
        var m = Modulate_Current;
        Raylib.DrawRectangleGradientEx(r,
            Color_Mul(top_left, m), Color_Mul(bottom_left, m),
            Color_Mul(bottom_right, m), Color_Mul(top_right, m));
    }

    // Alpha checkerboard: what a translucent colour is shown against so "half transparent
    // grey" can't be mistaken for "opaque grey".
    public static void RectChecker(Rectangle r, Color a, Color b, float cell)
    {
        Clip_Push(r);

        int cols = (int)MathF.Ceiling(r.Width / cell);
        int rows = (int)MathF.Ceiling(r.Height / cell);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Rect(new Rectangle(r.X + x * cell, r.Y + y * cell, cell, cell),
                     (x + y) % 2 == 0 ? a : b);
            }
        }

        Clip_Pop();
    }

    public static void Line(Vector2 a, Vector2 b, float thickness, Color c)
    {
        Raylib.DrawLineEx(a, b, thickness, Color_Mul(c, Modulate_Current));
    }

    public static void Texture(A_Texture tex, Rectangle dest, Color tint)
    {
        if (tex?.get_Texture2D() is not Texture2D t || t.Id == 0) return;

        var src = new Rectangle(0, 0, t.Width, t.Height);
        Raylib.DrawTexturePro(t, src, dest, Vector2.Zero, 0f, Color_Mul(tint, Modulate_Current));
    }

    // Draws with the style's shadow, then outline, then fill.
    public static void Text(string text, Vector2 pos, UIStyle_Text style)
    {
        if (string.IsNullOrEmpty(text)) return;
        style ??= ImpUITheme.game_theme.style_text;

        Font font = Font_Of(style);
        float size = style.size;
        float spacing = Text_Spacing(style);

        if (style.shadow_size > 0)
        {
            Raylib.DrawTextEx(font, text, pos + style.shadow_offset, size, spacing,
                Color_Mul(style.shadow_color, Modulate_Current));
        }

        if (style.outline_size > 0)
        {
            var oc = Color_Mul(style.outline_color, Modulate_Current);
            for (int dx = -style.outline_size; dx <= style.outline_size; dx++)
            {
                for (int dy = -style.outline_size; dy <= style.outline_size; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Raylib.DrawTextEx(font, text, pos + new Vector2(dx, dy), size, spacing, oc);
                }
            }
        }

        Raylib.DrawTextEx(font, text, pos, size, spacing, Color_Mul(style.color, Modulate_Current));
    }

    // Draws text vertically centred in r, horizontally placed by align (0=left, 0.5=centre, 1=right).
    public static void TextInRect(string text, Rectangle r, UIStyle_Text style, float align = 0.5f)
    {
        if (string.IsNullOrEmpty(text)) return;
        style ??= ImpUITheme.game_theme.style_text;

        var m = TextMeasure(text, style);
        var pos = new Vector2(
            r.X + (r.Width - m.X) * align,
            r.Y + (r.Height - m.Y) * 0.5f);

        Text(text, pos, style);
    }

    public static Vector2 TextMeasure(string text, UIStyle_Text style)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        style ??= ImpUITheme.game_theme.style_text;

        return Raylib.MeasureTextEx(Font_Of(style), text, style.size, Text_Spacing(style));
    }

    // The style's own font wins; otherwise the theme font; otherwise raylib's built-in,
    // which keeps text visible even if the font asset is missing off disc.
    static Font Font_Of(UIStyle_Text style)
    {
        var asset = style.font ?? ImpUITheme.game_theme.font;
        return asset?.get_Font() ?? Raylib.GetFontDefault();
    }

    static float Text_Spacing(UIStyle_Text style) => style.spacing;

    // ----------------------------------------------------------------
    // Input
    // ----------------------------------------------------------------

    // Roots are given in draw order; the last one drawn sits on top, so test in reverse.
    public static void Input_Process(params ImpComp[] roots)
    {
        var was_hovered = hovered;

        hovered = null;
        if (!Overlay_IsBlocking(mouse_pos))
        {
            for (int i = roots.Length - 1; i >= 0 && hovered == null; i--)
            {
                hovered = HitTest(roots[i], mouse_pos);
            }
        }

        if (was_hovered != hovered)
        {
            was_hovered?.Cursor_OnEntry(false);
            hovered?.Cursor_OnEntry(true);
        }

        if (mouse_pressed)
        {
            pressed = hovered;
            focused = hovered;
            hovered?.Cursor_OnEvent(ECursorEvent.Pressed);
        }
        else if (mouse_released)
        {
            hovered?.Cursor_OnEvent(ECursorEvent.Released);
            if (pressed != null && pressed == hovered) pressed.Cursor_OnEvent(ECursorEvent.Clicked);
            pressed = null;
        }

        if (wheel != 0f)
        {
            // bubble up to the nearest ancestor that actually scrolls
            for (ImpComp? c = hovered; c != null; c = c.parent)
            {
                if (!c.Cursor_WantsWheel()) continue;
                c.Cursor_OnEvent(ECursorEvent.Wheel);
                break;
            }
        }
    }

    // Children draw after their parent, so later children sit on top: walk in reverse
    // and take the first claim.
    static ImpComp2D? HitTest(ImpComp? comp, Vector2 p)
    {
        if (comp == null || !comp.is_visible) return null;

        var c2 = comp as ImpComp2D;
        if (c2 != null)
        {
            if (c2.cursor_filter == ECursorFilter.Ignore) return null;
            // a clipping comp can't be hit outside its own rect, and neither can its children
            if (c2.clip_contents && !Raylib.CheckCollisionPointRec(p, c2.rect)) return null;
        }

        for (int i = comp.children.Count - 1; i >= 0; i--)
        {
            var hit = HitTest(comp.children[i], p);
            if (hit != null) return hit;
        }

        if (c2 != null && c2.cursor_filter == ECursorFilter.Hit
            && Raylib.CheckCollisionPointRec(p, c2.rect))
        {
            return c2;
        }

        return null;
    }

    public static bool IsHovered(ImpComp2D c) => hovered == c;
    public static bool IsPressed(ImpComp2D c) => pressed == c && mouse_down;

    // ----------------------------------------------------------------
    // Rect helpers
    // ----------------------------------------------------------------

    public static Rectangle Rect_Intersect(Rectangle a, Rectangle b)
    {
        float x1 = MathF.Max(a.X, b.X);
        float y1 = MathF.Max(a.Y, b.Y);
        float x2 = MathF.Min(a.X + a.Width, b.X + b.Width);
        float y2 = MathF.Min(a.Y + a.Height, b.Y + b.Height);
        return new Rectangle(x1, y1, MathF.Max(0, x2 - x1), MathF.Max(0, y2 - y1));
    }

    public static Rectangle Rect_Inset(Rectangle r, TMargins m)
    {
        return new Rectangle(
            r.X + m.left,
            r.Y + m.top,
            MathF.Max(0, r.Width - m.left - m.right),
            MathF.Max(0, r.Height - m.top - m.bottom));
    }

    // ----------------------------------------------------------------
    // Colour helpers
    // ----------------------------------------------------------------

    public static Color Color_Mul(Color a, Color b)
    {
        return new Color(
            (byte)(a.R * b.R / 255),
            (byte)(a.G * b.G / 255),
            (byte)(a.B * b.B / 255),
            (byte)(a.A * b.A / 255));
    }

    public static Color Color_Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}
