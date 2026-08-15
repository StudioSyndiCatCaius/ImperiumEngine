using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Common = true)]
public class C2_Box : Imp2D
{
    [ImpVar] public UiStyle_Box style=UiStyle_Box.STYLE_BKG_DARK;
    
    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (style == null) return;
        style.Draw(Dimensions_Get());
    }
}

public class UiStyle_Box : ImpAsset
{
    public static UiStyle_Box STYLE_BKG_DARK = new()
    {
        tint = new Color(65, 65, 65, 255),
    };
    
    public static UiStyle_Box STYLE_BKG_MID = new()
    {
        tint = new Color(100, 100, 100, 255),
    };
    
    public static UiStyle_Box STYLE_BKG_LIGHT = new()
    {
        tint = new Color(200, 200, 200, 255),
    };

    public static UiStyle_Box STYLE_BTN_IDLE = new()
    {
        texture = A_Texture.BTN_A,
        tint = new Color(200, 200, 200, 255),
    };
    public static UiStyle_Box STYLE_BTN_HOVER = new()
    {
        texture = A_Texture.BTN_A,
        tint = new Color(0, 120, 215, 255),
    };
    public static UiStyle_Box STYLE_BTN_PRESS = new()
    {
        texture = A_Texture.BTN_A,
        tint = new Color(0, 84, 153, 255),
    };
    
    public static UiStyle_Box STYLE_TAB_IDLE = new()
    {
        texture = A_Texture.TAB_A,
        tint = new Color(200, 200, 200, 255),
    };
    public static UiStyle_Box STYLE_TAB_HOVER = new()
    {
        texture = A_Texture.TAB_A,
        tint = new Color(0, 120, 215, 255),
    };
    public static UiStyle_Box STYLE_TAB_PRESS = new()
    {
        texture = A_Texture.TAB_A,
        tint = new Color(0, 84, 153, 255),
    };

    [ImpVar] public A_Texture? texture = A_Texture.PANEL_B;
    [ImpVar] public Color tint=new Color(50,50,50,100);
    [ImpVar] public TMargins margins_outer; //offset drawn box from dimension edges
    [ImpVar] public TMargins margins_inner; //when used as a container, offset inner box from dimension edges (as in, children margins)
    [ImpVar] public bool is_nine_slice=true;
    [ImpVar] public TMargins nine_slice_margins=new (10,10,10,10);

    public void Draw(TDimensions2 dim)
    {
        Vector2 pos = dim.position + new Vector2(margins_outer.left, margins_outer.top);
        Vector2 sz = dim.size - new Vector2(
            margins_outer.left + margins_outer.right,
            margins_outer.top + margins_outer.bottom);
        if (sz.X <= 0 || sz.Y <= 0) return;

        if (texture == null)
        {
            Raylib.DrawRectangleV(pos, sz, tint);
            return;
        }

        Texture2D tex = texture.texture;
        if (!is_nine_slice)
        {
            Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(pos.X, pos.Y, sz.X, sz.Y), Vector2.Zero, 0f, tint);
            return;
        }

        float l = nine_slice_margins.left, r = nine_slice_margins.right;
        float t = nine_slice_margins.top, b = nine_slice_margins.bottom;
        float tw = tex.Width, th = tex.Height;
        float cx = MathF.Max(0, tw - l - r), cy = MathF.Max(0, th - t - b);
        float dx = MathF.Max(0, sz.X - l - r), dy = MathF.Max(0, sz.Y - t - b);
        float px = pos.X, py = pos.Y;

        void Patch(float sx, float sy, float sw, float sh, float dx_, float dy_, float dw, float dh)
        {
            if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0) return;
            Raylib.DrawTexturePro(tex, new Rectangle(sx, sy, sw, sh), new Rectangle(dx_, dy_, dw, dh), Vector2.Zero, 0f, tint);
        }

        // corners
        Patch(0, 0, l, t, px, py, l, t);
        Patch(tw - r, 0, r, t, px + sz.X - r, py, r, t);
        Patch(0, th - b, l, b, px, py + sz.Y - b, l, b);
        Patch(tw - r, th - b, r, b, px + sz.X - r, py + sz.Y - b, r, b);
        // edges
        Patch(l, 0, cx, t, px + l, py, dx, t);
        Patch(l, th - b, cx, b, px + l, py + sz.Y - b, dx, b);
        Patch(0, t, l, cy, px, py + t, l, dy);
        Patch(tw - r, t, r, cy, px + sz.X - r, py + t, r, dy);
        // center
        Patch(l, t, cx, cy, px + l, py + t, dx, dy);
    }
}