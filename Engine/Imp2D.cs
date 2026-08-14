using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;

public class Imp2D
{
    static readonly Stack<Rectangle> _clip = new();

    public static void Clip_Push(TDimensions2 dim)
    {
        Vector2 dpi = Raylib.GetWindowScaleDPI();
        if (dpi.X <= 0) dpi.X = 1;
        if (dpi.Y <= 0) dpi.Y = 1;
        float x = dim.position.X, y = dim.position.Y, w = dim.size.X, h = dim.size.Y;
        if (_clip.Count > 0)
        {
            Rectangle p = _clip.Peek();
            float x2 = MathF.Min(x + w, p.X + p.Width);
            float y2 = MathF.Min(y + h, p.Y + p.Height);
            x = MathF.Max(x, p.X);
            y = MathF.Max(y, p.Y);
            w = x2 - x;
            h = y2 - y;
        }
        if (w < 0) w = 0;
        if (h < 0) h = 0;
        Rectangle r = new(x, y, w, h);
        _clip.Push(r);
        Raylib.BeginScissorMode(
            (int)MathF.Floor(r.X * dpi.X),
            (int)MathF.Floor(r.Y * dpi.Y),
            (int)MathF.Ceiling(r.Width * dpi.X),
            (int)MathF.Ceiling(r.Height * dpi.Y));
    }

    public static void Clip_Pop()
    {
        if (_clip.Count == 0) return;
        _clip.Pop();
        if (_clip.Count == 0)
        {
            Raylib.EndScissorMode();
            return;
        }
        Rectangle r = _clip.Peek();
        Vector2 dpi = Raylib.GetWindowScaleDPI();
        if (dpi.X <= 0) dpi.X = 1;
        if (dpi.Y <= 0) dpi.Y = 1;
        Raylib.BeginScissorMode(
            (int)MathF.Floor(r.X * dpi.X),
            (int)MathF.Floor(r.Y * dpi.Y),
            (int)MathF.Ceiling(r.Width * dpi.X),
            (int)MathF.Ceiling(r.Height * dpi.Y));
    }

    // Turns the GL matrix about the dimension's pivot so plain unrotated draw calls
    // (nine-slice patches, tiles, text) come out rotated. Always pair with Rotate_Pop.
    public static bool Rotate_Push(TDimensions2 dim)
    {
        if (!dim.IsRotated) return false;
        Vector2 o = dim.Pivot_Point;
        Rlgl.PushMatrix();
        Rlgl.Translatef(o.X, o.Y, 0f);
        Rlgl.Rotatef(dim.rotation, 0f, 0f, 1f);
        Rlgl.Translatef(-o.X, -o.Y, 0f);
        return true;
    }

    public static void Rotate_Pop(bool pushed)
    {
        if (pushed) Rlgl.PopMatrix();
    }

    public static void Draw_Box(Vector2 position, Vector2 size, Color color)
    {
        Raylib.DrawRectangleV(position, size, color);
    }

    public static void Draw_BoxStyle(Vector2 position, Vector2 size, UiStyle_Box style)
    {
        if (style == null) return;

        if (style.texture == null)
        {
            Raylib.DrawRectangleV(position, size, style.tint);
            return;
        }

        Texture2D tex = style.texture.texture;
        Rectangle src = new(0, 0, tex.Width, tex.Height);
        Rectangle dest = new(position.X, position.Y, size.X, size.Y);
        Raylib.DrawTexturePro(tex, src, dest, Vector2.Zero, 0f, style.tint);
    }

    // reverse draw order: last child first, children before self
    public static ImpComp2D? Trace_Point(Vector2 pos, ImpComp node)
    {
        if (node == null || !node.is_visible) return null;
        if (node is ImpComp2D c2 && c2.cursor_filter == ECursorFilter.Ignore) return null;

        for (int i = node.children.Count - 1; i >= 0; i--)
        {
            ImpComp2D? hit = Trace_Point(pos, node.children[i]);
            if (hit != null) return hit;
        }

        if (node is ImpComp2D self && self.cursor_filter == ECursorFilter.Hit)
        {
            if (self.Dimensions_Get().Contains(pos)) return self;
        }
        return null;
    }
}