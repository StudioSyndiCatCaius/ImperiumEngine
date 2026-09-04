using System.Numerics;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Assets;

public class A_Texture : ImpAsset
{
    // ==============================================================================================================
    // STATIC
    // ==============================================================================================================
    
    
    // ==============================================================================================================
    // CLASS
    // ==============================================================================================================
    
    [ImpVar] public TextureFilter filter = TextureFilter.Trilinear;
    
    public Texture2D texture;
    
    public static string PATH_SKY_1 = "{engine}/2D/HDR/sky_1.ImpAsset";
    public static string PATH_SKY_2 = "{engine}/2D/HDR/sky_2.ImpAsset";

    public override void OnReimport(ImpFile src)
    {
        base.OnReimport(src);
        texture = src.get_Texture(0);
    }

    public void Draw(TBounds2 bounds, TTransform2 offset, EImageLayout layout = EImageLayout.Stretch, 
        TMargins nine_slice = default, TMargins clip_margins=default, bool clip_margins_as_ratio=false, Color? tint = null)
    {
        if (texture.Id == 0 && !string.IsNullOrEmpty(sourcefile)) Source_Reimport();
        if (texture.Id == 0 || bounds.IsEmpty) return;

        float x = MathF.Min(bounds.start.X, bounds.end.X);
        float y = MathF.Min(bounds.start.Y, bounds.end.Y);
        float dw = MathF.Abs(bounds.end.X - bounds.start.X);
        float dh = MathF.Abs(bounds.end.Y - bounds.start.Y);
        if (dw < 1e-4f || dh < 1e-4f) return;

        float cl = clip_margins.left, cr = clip_margins.right, ct = clip_margins.top, cb = clip_margins.bottom;
        if (clip_margins_as_ratio) { cl *= dw; cr *= dw; ct *= dh; cb *= dh; }
        if (cl < 0) cl = 0; if (cr < 0) cr = 0; if (ct < 0) ct = 0; if (cb < 0) cb = 0;
        float vx = x + cl, vy = y + ct, vw = dw - cl - cr, vh = dh - ct - cb;
        if (vw < 1e-4f || vh < 1e-4f) return;

        float tw = texture.Width;
        float th = texture.Height;
        if (tw < 1) tw = 1;
        if (th < 1) th = 1;

        Vector2 origin = offset.position;
        Vector2 pivot = new(x + origin.X, y + origin.Y);
        float rot = (float)offset.rotation;
        Color draw_tint = tint ?? Color.White;

        void Blit(Rectangle source, float tx, float ty, float w, float h)
        {
            if (w < 1e-4f || h < 1e-4f || source.Width < 1e-4f || source.Height < 1e-4f) return;
            Raylib.DrawTexturePro(texture, source,
                new Rectangle(pivot.X, pivot.Y, w, h),
                new Vector2(pivot.X - tx, pivot.Y - ty),
                rot, draw_tint);
        }

        switch (layout)
        {
            case EImageLayout.Tile:
            {
                float sx = offset.scale.X, sy = offset.scale.Y;
                if (sx <= 0) sx = 1;
                if (sy <= 0) sy = 1;
                float tile_w = tw * sx;
                float tile_h = th * sy;
                if (tile_w < 2 || tile_h < 2)
                {
                    Blit(new Rectangle(cl / dw * tw, ct / dh * th, vw / dw * tw, vh / dh * th), vx, vy, vw, vh);
                    break;
                }
                float end_x = vx + vw, end_y = vy + vh;
                for (float ty = y + MathF.Floor((vy - y) / tile_h) * tile_h; ty < end_y; ty += tile_h)
                for (float tx = x + MathF.Floor((vx - x) / tile_w) * tile_w; tx < end_x; tx += tile_w)
                {
                    float ix = MathF.Max(tx, vx);
                    float iy = MathF.Max(ty, vy);
                    float iw = MathF.Min(tx + tile_w, end_x) - ix;
                    float ih = MathF.Min(ty + tile_h, end_y) - iy;
                    Blit(new Rectangle((ix - tx) / sx, (iy - ty) / sy, iw / sx, ih / sy), ix, iy, iw, ih);
                }
                break;
            }
            case EImageLayout.NineSlice:
            {
                NPatchInfo np = new()
                {
                    Source = new Rectangle(0, 0, tw, th),
                    Left = (int)nine_slice.left,
                    Top = (int)nine_slice.top,
                    Right = (int)nine_slice.right,
                    Bottom = (int)nine_slice.bottom,
                    Layout = NPatchLayout.NinePatch,
                };
                Raylib.DrawTextureNPatch(texture, np,
                    new Rectangle(pivot.X, pivot.Y, vw, vh),
                    new Vector2(pivot.X - vx, pivot.Y - vy),
                    rot, draw_tint);
                break;
            }
            case EImageLayout.Retain_Fit:
            {
                float s = MathF.Min(dw / tw, dh / th);
                float w = tw * s, h = th * s;
                float fx = x + (dw - w) * 0.5f;
                float fy = y + (dh - h) * 0.5f;
                float ix = MathF.Max(fx, vx);
                float iy = MathF.Max(fy, vy);
                float iw = MathF.Min(fx + w, vx + vw) - ix;
                float ih = MathF.Min(fy + h, vy + vh) - iy;
                Blit(new Rectangle((ix - fx) / s, (iy - fy) / s, iw / s, ih / s), ix, iy, iw, ih);
                break;
            }
            case EImageLayout.Retain_Fill:
            {
                float s = MathF.Max(dw / tw, dh / th);
                Blit(new Rectangle((tw - dw / s) * 0.5f + cl / s, (th - dh / s) * 0.5f + ct / s, vw / s, vh / s), vx, vy, vw, vh);
                break;
            }
            default:
                Blit(new Rectangle(cl / dw * tw, ct / dh * th, vw / dw * tw, vh / dh * th), vx, vy, vw, vh);
                break;
        }
    }
    
    // ===============================================================================================================
    // STATICS
    // ===============================================================================================================
    [Builtin] public static A_Texture UI_BTN = GAsset.ImportSource<A_Texture>("{engine}/2D/UI/UI_btn_D.png");
    [Builtin] public static A_Texture UI_BOX_LIGHT = GAsset.ImportSource<A_Texture>("{engine}/2D/UI/UI_box_L_B.png");
    [Builtin] public static A_Texture UI_BOX_DARK = GAsset.ImportSource<A_Texture>("{engine}/2D/UI/UI_box_D_B.png");
    
}