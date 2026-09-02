using System.Numerics;
using Engine.Core;
using Engine.Enums;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Assets;

public class A_Font : ImpAsset
{
    [ImpVar] public int source_id=0;
    [ImpVar] public int size=12;
    [ImpVar] public Color color=Color.White;
    [ImpVar] public int spacing=0;
    [ImpVar] public int shadow_size=0;
    [ImpVar] public Color shadow_color=Color.Black;
    [ImpVar] public Vector2 shadow_offset=Vector2.Zero;
    [ImpVar] public int outline_size=0;
    [ImpVar] public Color outline_color=Color.Black;

    public Font font;
    
    public override void OnReimport(ImpFile src)
    {
        base.OnReimport(src);
        font = src.get_Font(source_id);
    }

    public Font Resolve()
    {
        if (font.Texture.Id != 0) return font;
        if (!string.IsNullOrEmpty(sourcefile) && Source_Get() == null)
            Source_Reimport();
        if (font.Texture.Id != 0) return font;
        return Raylib.GetFontDefault();
    }

    float PixelSize(TTransform2 offset = default)
    {
        float px = size > 0 ? size : 12;
        if (offset.scale.X > 0 && offset.scale.X != 1) px *= offset.scale.X;
        return px;
    }

    public Vector2 Measure(string text, float max_width = 0, ETextWrap wrap = ETextWrap.None)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        Font f = Resolve();
        float px = PixelSize();
        List<string> lines = Break(text, f, px, max_width, wrap);
        float w = 0;
        float lh = LineHeight(f, px);
        for (int i = 0; i < lines.Count; i++)
        {
            Vector2 sz = Raylib.MeasureTextEx(f, lines[i], px, spacing);
            if (sz.X > w) w = sz.X;
        }
        return new Vector2(w, lh * lines.Count);
    }

    public void Draw(string text, TBounds2 bounds, TTransform2 offset, ETextWrap wrap)
    {
        if (string.IsNullOrEmpty(text) || bounds.IsEmpty) return;
        Font f = Resolve();
        if (f.Texture.Id == 0) return;

        float px = PixelSize(offset);
        float x0 = MathF.Min(bounds.start.X, bounds.end.X);
        float y0 = MathF.Min(bounds.start.Y, bounds.end.Y);
        float max_w = MathF.Abs(bounds.end.X - bounds.start.X);
        float lh = LineHeight(f, px);
        List<string> lines = Break(text, f, px, max_w, wrap);
        Vector2 pivot = new(x0 + offset.position.X, y0 + offset.position.Y);
        float rot = (float)offset.rotation;

        for (int i = 0; i < lines.Count; i++)
        {
            Vector2 pos = new(x0, y0 + i * lh);
            Vector2 origin = pivot - pos;
            Blit(f, lines[i], pivot, origin, rot, px);
        }
    }

    void Blit(Font f, string line, Vector2 pivot, Vector2 origin, float rot, float px)
    {
        if (shadow_size > 0 || shadow_offset != Vector2.Zero)
        {
            Vector2 s = origin - shadow_offset;
            if (shadow_size <= 0)
                Raylib.DrawTextPro(f, line, pivot, s, rot, px, spacing, shadow_color);
            else
                for (int dy = -shadow_size; dy <= shadow_size; dy++)
                for (int dx = -shadow_size; dx <= shadow_size; dx++)
                    Raylib.DrawTextPro(f, line, pivot, s - new Vector2(dx, dy), rot, px, spacing, shadow_color);
        }
        if (outline_size > 0)
        {
            for (int dy = -outline_size; dy <= outline_size; dy++)
            for (int dx = -outline_size; dx <= outline_size; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                Raylib.DrawTextPro(f, line, pivot, origin - new Vector2(dx, dy), rot, px, spacing, outline_color);
            }
        }
        Raylib.DrawTextPro(f, line, pivot, origin, rot, px, spacing, color);
    }

    float LineHeight(Font f, float px)
    {
        float h = Raylib.MeasureTextEx(f, " ", px, spacing).Y;
        return h > 1e-4f ? h : px;
    }

    List<string> Break(string text, Font f, float px, float max_w, ETextWrap wrap)
    {
        List<string> lines = new();
        string[] paras = text.Replace("\r\n", "\n").Split('\n');
        for (int p = 0; p < paras.Length; p++)
        {
            string para = paras[p];
            if (wrap == ETextWrap.None || max_w <= 1e-4f)
            {
                lines.Add(para);
                continue;
            }
            if (para.Length == 0)
            {
                lines.Add("");
                continue;
            }
            if (wrap == ETextWrap.Word) PackWords(para, f, px, max_w, lines);
            else PackChars(para, f, px, max_w, lines);
        }
        return lines;
    }

    void PackWords(string para, Font f, float px, float max_w, List<string> lines)
    {
        string[] words = para.Split(' ');
        string line = "";
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            string trial = line.Length == 0 ? word : line + " " + word;
            if (Raylib.MeasureTextEx(f, trial, px, spacing).X <= max_w)
            {
                line = trial;
                continue;
            }
            if (line.Length > 0) lines.Add(line);
            if (Raylib.MeasureTextEx(f, word, px, spacing).X <= max_w) line = word;
            else
            {
                PackChars(word, f, px, max_w, lines);
                line = "";
            }
        }
        if (line.Length > 0) lines.Add(line);
    }

    void PackChars(string s, Font f, float px, float max_w, List<string> lines)
    {
        string line = "";
        for (int i = 0; i < s.Length; i++)
        {
            string trial = line + s[i];
            if (line.Length > 0 && Raylib.MeasureTextEx(f, trial, px, spacing).X > max_w)
            {
                lines.Add(line);
                line = s[i].ToString();
            }
            else line = trial;
        }
        if (line.Length > 0) lines.Add(line);
    }
    
    
    // ================================================================================================================
    // STATIC
    // ================================================================================================================

    // ----------------------------------------------------
    // Arial
    // ----------------------------------------------------
    [Builtin] public static A_Font ARIAL_P = new()
    {
        sourcefile = "{engine}/Fonts/Arial/Arial.ttf",
        size = 16,
        color = Color.White,
    };

    // ----------------------------------------------------
    // Tenderness
    // ----------------------------------------------------
    [Builtin] public static A_Font TENDERNESS_P = new()
    {
        sourcefile = "{engine}/Fonts/Tenderness/tenderness.otf",
        size = 16,
        color = Color.White,
    };
}
