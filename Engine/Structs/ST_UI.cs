using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using Raylib_cs;

namespace ImperiumEngine.Structs;

public struct TImage
{
    public A_Texture texture;
    public Color tint=Color.White;
    public EImageLayout layout;
    public float tiling = 1.0f;
    public TMargins edge_margins; //offset from edge
    public TMargins nine_slice_margins; //used for nine slice
    public bool clip_to_bounds = true; //mainly for EImageLayout.Retain_Fill, this clips the image to the bounds of the dimension

    public TImage()
    {
        texture = null;
        layout = EImageLayout.Stretch;
    }

    public void Draw(TDimensions2 dim)
    {
        if (texture == null) return;
        Texture2D tex = texture.texture;
        if (tex.Id == 0 || tex.Width <= 0 || tex.Height <= 0) return;

        Vector2 pos = dim.position + new Vector2(edge_margins.left, edge_margins.top);
        Vector2 sz = dim.size - new Vector2(
            edge_margins.left + edge_margins.right,
            edge_margins.top + edge_margins.bottom);
        if (sz.X <= 0 || sz.Y <= 0) return;

        float tw = tex.Width, th = tex.Height;
        Color c = tint;

        switch (layout)
        {
            case EImageLayout.Tile:
            {
                float tile_w = tw * (tiling > 0 ? tiling : 1f);
                float tile_h = th * (tiling > 0 ? tiling : 1f);
                for (float y = 0; y < sz.Y; y += tile_h)
                for (float x = 0; x < sz.X; x += tile_w)
                {
                    float dw = MathF.Min(tile_w, sz.X - x);
                    float dh = MathF.Min(tile_h, sz.Y - y);
                    Raylib.DrawTexturePro(tex,
                        new Rectangle(0, 0, tw * (dw / tile_w), th * (dh / tile_h)),
                        new Rectangle(pos.X + x, pos.Y + y, dw, dh),
                        Vector2.Zero, 0f, c);
                }
                break;
            }
            case EImageLayout.NineSlice:
            {
                float l = nine_slice_margins.left, r = nine_slice_margins.right;
                float t = nine_slice_margins.top, b = nine_slice_margins.bottom;
                float cx = MathF.Max(0, tw - l - r), cy = MathF.Max(0, th - t - b);
                float dx = MathF.Max(0, sz.X - l - r), dy = MathF.Max(0, sz.Y - t - b);
                float px = pos.X, py = pos.Y;
                void Patch(float sx, float sy, float sw, float sh, float dx_, float dy_, float dw, float dh)
                {
                    if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0) return;
                    Raylib.DrawTexturePro(tex, new Rectangle(sx, sy, sw, sh), new Rectangle(dx_, dy_, dw, dh), Vector2.Zero, 0f, c);
                }
                Patch(0, 0, l, t, px, py, l, t);
                Patch(tw - r, 0, r, t, px + sz.X - r, py, r, t);
                Patch(0, th - b, l, b, px, py + sz.Y - b, l, b);
                Patch(tw - r, th - b, r, b, px + sz.X - r, py + sz.Y - b, r, b);
                Patch(l, 0, cx, t, px + l, py, dx, t);
                Patch(l, th - b, cx, b, px + l, py + sz.Y - b, dx, b);
                Patch(0, t, l, cy, px, py + t, l, dy);
                Patch(tw - r, t, r, cy, px + sz.X - r, py + t, r, dy);
                Patch(l, t, cx, cy, px + l, py + t, dx, dy);
                break;
            }
            case EImageLayout.Retain_Fit:
            {
                float scale = MathF.Min(sz.X / tw, sz.Y / th);
                float dw = tw * scale, dh = th * scale;
                Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tw, th),
                    new Rectangle(pos.X + (sz.X - dw) * 0.5f, pos.Y + (sz.Y - dh) * 0.5f, dw, dh),
                    Vector2.Zero, 0f, c);
                break;
            }
            case EImageLayout.Retain_Fill:
            {
                float scale = MathF.Max(sz.X / tw, sz.Y / th);
                if (clip_to_bounds)
                {
                    float sw = sz.X / scale, sh = sz.Y / scale;
                    Raylib.DrawTexturePro(tex,
                        new Rectangle((tw - sw) * 0.5f, (th - sh) * 0.5f, sw, sh),
                        new Rectangle(pos.X, pos.Y, sz.X, sz.Y),
                        Vector2.Zero, 0f, c);
                }
                else
                {
                    float dw = tw * scale, dh = th * scale;
                    Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tw, th),
                        new Rectangle(pos.X + (sz.X - dw) * 0.5f, pos.Y + (sz.Y - dh) * 0.5f, dw, dh),
                        Vector2.Zero, 0f, c);
                }
                break;
            }
            default:
                Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tw, th),
                    new Rectangle(pos.X, pos.Y, sz.X, sz.Y), Vector2.Zero, 0f, c);
                break;
        }
    }
}

public struct TMargins
{
    public float left, right, top, bottom;
    
    public TMargins(float left, float right, float top, float bottom)
    {
        this.left = left;
        this.right = right;
        this.top = top;
        this.bottom = bottom;
    }
}

public struct TDimensions2
{
    public Vector2 position; //top-left of the unrotated box
    public Vector2 size;
    public float rotation;   //degrees, turned about (position + pivot)
    public Vector2 pivot;    //pixel offset from position to the pivot point

    public Vector2 Pivot_Point => position + pivot;

    public bool IsRotated => MathF.Abs(rotation) > 1e-4f;

    public bool Contains(Vector2 p)
    {
        if (!IsRotated)
        {
            return p.X >= position.X && p.Y >= position.Y
                && p.X < position.X + size.X && p.Y < position.Y + size.Y;
        }
        // Undo the rotation about the pivot, then test the plain box.
        Vector2 o = Pivot_Point;
        float rad = -rotation * (MathF.PI / 180f);
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        Vector2 d = p - o;
        Vector2 l = o + new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        return l.X >= position.X && l.Y >= position.Y
            && l.X < position.X + size.X && l.Y < position.Y + size.Y;
    }
}