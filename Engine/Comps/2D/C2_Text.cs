using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Enums;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

[ImpClass(Common = true)]
public class C2_Text : Imp2D
{
    [ImpVar] public UI_Text style=UI_Text.DEFAULT;
    [ImpVar] public string text="";
    [ImpVar] public int font_size_override=0;
    [ImpVar] public ETextWrap wrap=ETextWrap.Word;
    [ImpVar] public EUIPositionAlignment text_alignment_v;
    [ImpVar] public EUIPositionAlignment text_alignment_h;
    
    public override void OnDraw2D(double dt, EDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);
        if (style == null) return;
        var dim = Dimensions_Get();
        style.Draw(text, dim.position, dim.size, font_size_override, wrap, text_alignment_v, text_alignment_h);
    }
}

// ####################################################################################################################
// STYLE
// ####################################################################################################################

public class UI_Text : ImpAsset
{
    public static UI_Text DEFAULT = new();
    public static UI_Text LIGHT = new()
    {
        size = 13,
        color = Color.White,
    };
    public static UI_Text MUTED = new()
    {
        size = 11,
        color = new Color(170, 170, 170, 255),
    };
    public static UI_Text STLYE_H1 = new() { size = 24, };
    public static UI_Text STLYE_H2 = new() { size = 18, };
    public static UI_Text STLYE_H3 = new() { size = 14, };
    public static UI_Text STLYE_PARAGRAPH = new() { size = 12, };
    public static UI_Text STLYE_TINY = new() { size = 8, };
    
    [ImpVar] public A_Font font=A_Font.FONT_ARIAL;
    [ImpVar] public int size=16;
    [ImpVar] public Color color=Color.White;
    
    [ImpVar] public int outline_size=0; //if 0, no outline
    [ImpVar] public Color outline_color=Color.Black;
    
    [ImpVar] public Color shadow_color=Color.Black;
    [ImpVar] public Vector2 shadow_offset=Vector2.Zero; // if 0, no shadow

    const float Spacing = 1f;

    public void Draw(string text, Vector2 position, Vector2 size, int size_override = 0, ETextWrap wrap = ETextWrap.Word,
        EUIPositionAlignment alignment_v = EUIPositionAlignment.Center, EUIPositionAlignment alignment_h = EUIPositionAlignment.Center)
    {
        if (string.IsNullOrEmpty(text) || size.X <= 0 || size.Y <= 0) return;

        Font f = font != null ? font.font : Raylib.GetFontDefault();
        float font_size = size_override > 0 ? size_override : this.size;
        if (font_size <= 0) return;

        List<string> lines = Wrap(text, f, font_size, size.X, wrap);
        if (lines.Count == 0) return;

        float line_h = Raylib.MeasureTextEx(f, "Ay", font_size, Spacing).Y;
        if (line_h <= 0) line_h = font_size;
        float block_h = lines.Count * line_h;
        float y0 = Align(alignment_v, position.Y, size.Y, block_h);

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            float line_w = Raylib.MeasureTextEx(f, line, font_size, Spacing).X;
            float x = Align(alignment_h, position.X, size.X, line_w);
            float y = y0 + i * line_h;
            Vector2 p = new(x, y);

            if (shadow_offset != Vector2.Zero)
                Raylib.DrawTextEx(f, line, p + shadow_offset, font_size, Spacing, shadow_color);

            if (outline_size > 0)
            {
                int o = outline_size;
                for (int ox = -o; ox <= o; ox++)
                for (int oy = -o; oy <= o; oy++)
                {
                    if (ox == 0 && oy == 0) continue;
                    Raylib.DrawTextEx(f, line, new Vector2(x + ox, y + oy), font_size, Spacing, outline_color);
                }
            }

            Raylib.DrawTextEx(f, line, p, font_size, Spacing, color);
        }
    }

    static float Align(EUIPositionAlignment a, float origin, float avail, float content) => a switch
    {
        EUIPositionAlignment.Center => origin + (avail - content) * 0.5f,
        EUIPositionAlignment.End => origin + avail - content,
        _ => origin,
    };

    static List<string> Wrap(string text, Font font, float font_size, float max_width, ETextWrap wrap)
    {
        var lines = new List<string>();
        string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');

        foreach (string paragraph in paragraphs)
        {
            if (wrap == ETextWrap.None || string.IsNullOrEmpty(paragraph))
            {
                lines.Add(paragraph);
                continue;
            }

            if (wrap == ETextWrap.Word)
                WrapWord(paragraph, font, font_size, max_width, lines);
            else
                WrapArbitrary(paragraph, font, font_size, max_width, lines);
        }

        return lines;
    }

    static void WrapWord(string text, Font font, float font_size, float max_width, List<string> lines)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        string line = words[0];
        for (int i = 1; i < words.Length; i++)
        {
            string trial = line + " " + words[i];
            if (Raylib.MeasureTextEx(font, trial, font_size, Spacing).X <= max_width)
                line = trial;
            else
            {
                lines.Add(line);
                line = words[i];
            }
        }
        lines.Add(line);
    }

    static void WrapArbitrary(string text, Font font, float font_size, float max_width, List<string> lines)
    {
        if (text.Length == 0)
        {
            lines.Add(string.Empty);
            return;
        }

        int start = 0;
        for (int i = 1; i <= text.Length; i++)
        {
            string slice = text[start..i];
            if (Raylib.MeasureTextEx(font, slice, font_size, Spacing).X > max_width)
            {
                if (i - start <= 1)
                {
                    lines.Add(slice);
                    start = i;
                }
                else
                {
                    lines.Add(text[start..(i - 1)]);
                    start = i - 1;
                    i = start;
                }
            }
        }
        if (start < text.Length)
            lines.Add(text[start..]);
    }
}