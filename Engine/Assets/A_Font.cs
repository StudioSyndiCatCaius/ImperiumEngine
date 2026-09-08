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

    readonly List<string> _wrap_lines = new();
    float[] _wrap_widths = Array.Empty<float>();
    
    public override void OnReimport(ImpFile src)
    {
        base.OnReimport(src);
        font = src.get_Font(source_id);
    }

    public Font Resolve()
    {
        if (font.Texture.Id != 0) return font;
        if (!string.IsNullOrEmpty(sourcefile)) Source_Get();
        if (font.Texture.Id != 0) return font;
        return Raylib.GetFontDefault();
    }

    public Vector2 Measure(string text, float max_width = 0, ETextWrap wrap = ETextWrap.None)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        Font f = Resolve();
        float px = PixelSize();
        Wrap(text, f, px, max_width, wrap);
        float w = 0;
        float lh = LineHeight(f, px);
        for (int i = 0; i < _wrap_lines.Count; i++)
        {
            float lw = i < _wrap_widths.Length ? _wrap_widths[i] : Raylib.MeasureTextEx(f, _wrap_lines[i], px, spacing).X;
            if (lw > w) w = lw;
        }
        return new Vector2(w, lh * _wrap_lines.Count);
    }

    public void Draw(string text, TBounds2 bounds, ETextWrap wrap, TLayoutAlignment text_align) //text_align is where to align & build the text from within the bounds
    {
        if (string.IsNullOrEmpty(text) || bounds.IsEmpty) return;
        Font f = Resolve();
        if (f.Texture.Id == 0) return;

        float px = PixelSize();
        float x0 = MathF.Min(bounds.start.X, bounds.end.X);
        float y0 = MathF.Min(bounds.start.Y, bounds.end.Y);
        float vw = MathF.Abs(bounds.end.X - bounds.start.X);
        float vh = MathF.Abs(bounds.end.Y - bounds.start.Y);
        if (vw < 1e-4f || vh < 1e-4f) return;

        float lh = LineHeight(f, px);
        Wrap(text, f, px, vw, wrap);
        int n = _wrap_lines.Count;
        if (n == 0) return;

        if (_wrap_widths.Length < n) _wrap_widths = new float[n];
        float block_w = 0;
        for (int i = 0; i < n; i++)
        {
            _wrap_widths[i] = Raylib.MeasureTextEx(f, _wrap_lines[i], px, spacing).X;
            if (_wrap_widths[i] > block_w) block_w = _wrap_widths[i];
        }

        float block_h = lh * n;
        float gap = 0;
        if (text_align.align_V == ELayoutAlignment.Fill && n > 1 && block_h < vh)
            gap = (vh - block_h) / (n - 1);

        float used_h = block_h + gap * Math.Max(0, n - 1);
        float y = Align(text_align.align_V, y0, vh, used_h);

        for (int i = 0; i < n; i++)
        {
            float x = Align(text_align.align_H, x0, vw, _wrap_widths[i]);
            Blit(f, _wrap_lines[i], new Vector2(x, y), px);
            y += lh + gap;
        }
    }

    static float Align(ELayoutAlignment a, float origin, float view, float content)
        => a switch
        {
            ELayoutAlignment.Center => origin + (view - content) * 0.5f,
            ELayoutAlignment.End    => origin + view - content,
            _ => origin, // Start, Fill: build from the origin edge
        };

    float PixelSize() => size > 0 ? size : 12;

    float LineHeight(Font f, float px)
    {
        float h = Raylib.MeasureTextEx(f, "Ag", px, spacing).Y;
        return h > 1e-4f ? h : px;
    }

    void Blit(Font f, string line, Vector2 pos, float px)
    {
        if (shadow_size > 0 || shadow_offset != Vector2.Zero)
        {
            Vector2 s = pos + shadow_offset;
            Color sc = Imp2D.DrawTint(shadow_color);
            if (shadow_size <= 0)
                Raylib.DrawTextEx(f, line, s, px, spacing, sc);
            else
                for (int dy = -shadow_size; dy <= shadow_size; dy++)
                for (int dx = -shadow_size; dx <= shadow_size; dx++)
                    Raylib.DrawTextEx(f, line, s + new Vector2(dx, dy), px, spacing, sc);
        }
        if (outline_size > 0)
        {
            Color oc = Imp2D.DrawTint(outline_color);
            for (int dy = -outline_size; dy <= outline_size; dy++)
            for (int dx = -outline_size; dx <= outline_size; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                Raylib.DrawTextEx(f, line, pos + new Vector2(dx, dy), px, spacing, oc);
            }
        }
        Raylib.DrawTextEx(f, line, pos, px, spacing, Imp2D.DrawTint(color));
    }

    void Wrap(string text, Font f, float px, float max_w, ETextWrap wrap)
    {
        _wrap_lines.Clear();
        bool wrap_on = wrap != ETextWrap.None && max_w > 1e-4f;
        int start = 0;
        while (start <= text.Length)
        {
            int nl = text.IndexOf('\n', start);
            if (nl < 0) nl = text.Length;
            int end = nl;
            if (end > start && text[end - 1] == '\r') end--;
            string para = start == 0 && end == text.Length ? text : text[start..end];
            if (!wrap_on) _wrap_lines.Add(para);
            else if (para.Length == 0) _wrap_lines.Add("");
            else if (wrap == ETextWrap.Word) PackWords(para, f, px, max_w, _wrap_lines);
            else PackChars(para, f, px, max_w, _wrap_lines);
            if (nl == text.Length) break;
            start = nl + 1;
        }
    }

    void PackWords(string para, Font f, float px, float max_w, List<string> lines)
    {
        string line = "";
        int i = 0;
        int words = 0;
        while (i <= para.Length)
        {
            int sp = para.IndexOf(' ', i);
            if (sp < 0) sp = para.Length;
            string word = para[i..sp];
            words++;
            string trial = line.Length == 0 ? word : line + " " + word;
            if (Raylib.MeasureTextEx(f, trial, px, spacing).X <= max_w)
            {
                line = trial;
            }
            else
            {
                if (line.Length > 0) lines.Add(line);
                if (Raylib.MeasureTextEx(f, word, px, spacing).X <= max_w) line = word;
                else
                {
                    PackChars(word, f, px, max_w, lines);
                    line = "";
                }
            }
            if (sp == para.Length) break;
            i = sp + 1;
        }
        if (line.Length > 0) lines.Add(line);
        else if (words == 0) lines.Add("");
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
