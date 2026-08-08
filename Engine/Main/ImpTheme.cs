using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using Raylib_cs;

namespace ImperiumEngine.Main;

// A theme is the palette + metrics + font that a subtree of ImpComp2D draws with.
//
// Assign one to any comp via ImpComp2D.theme and it cascades to every descendant that
// doesn't set its own - the same model as Godot's Theme resource. Comps resolve theirs
// with Theme_Get(), which walks up the tree and lands on `game_theme` when nothing is set.
//
// The file only stores palette/metrics/font. The UIStyle_* objects components actually
// draw with are derived in Build(), so a theme file stays small and internally consistent.

public class ImpUITheme : ImpAsset
{
    // ==========================================================================================
    // Statics
    // ==========================================================================================
    public const string path_dark  = "{engine}/Themes/ThemeDark.ImpTheme";
    public const string path_light = "{engine}/Themes/ThemeLight.ImpTheme";

    // Built in code, so there is always something to draw with even when the theme files
    // are missing off disc. Must stay declared before the slots below, which fall back to it.
    public static readonly ImpUITheme fallback = new ImpUITheme { display_name = "Fallback" };

    //the editor assigns this to its root comp; game UI falls back to game_theme
    public static ImpUITheme editor_theme = Load<ImpUITheme>(path_dark) ?? fallback;
    public static ImpUITheme game_theme   = Load<ImpUITheme>(path_dark) ?? fallback;

    // ==========================================================================================
    // Class
    // ==========================================================================================
    [ImpVar] public string display_name = "";

    // ----------------------------------------------------------------
    // Font
    // ----------------------------------------------------------------
    [ImpVar] public string font_file = "{engine}/Fonts/FNT_Arial.ImpAsset";
    [ImpVar] public int font_size = 16;
    [ImpVar] public float font_spacing = 1f;

    // ----------------------------------------------------------------
    // Palette
    // ----------------------------------------------------------------
    [ImpVar] public Color col_background = new Color(30, 30, 34, 255);
    [ImpVar] public Color col_panel      = new Color(42, 42, 48, 255);
    [ImpVar] public Color col_panel_alt  = new Color(54, 54, 62, 255);
    [ImpVar] public Color col_accent     = new Color(64, 118, 200, 255);
    [ImpVar] public Color col_hover      = new Color(72, 72, 84, 255);
    [ImpVar] public Color col_pressed    = new Color(28, 28, 34, 255);
    [ImpVar] public Color col_disabled   = new Color(46, 46, 50, 255);
    [ImpVar] public Color col_text       = new Color(220, 220, 226, 255);
    [ImpVar] public Color col_text_dim   = new Color(140, 140, 150, 255);
    [ImpVar] public Color col_line       = new Color(72, 72, 82, 255);

    // ----------------------------------------------------------------
    // Metrics
    // ----------------------------------------------------------------
    [ImpVar] public float menubar_height  = 28;
    [ImpVar] public float tab_height      = 30;
    [ImpVar] public float item_height     = 26;
    [ImpVar] public float padding         = 8;
    [ImpVar] public float scrollbar_width = 12;
    [ImpVar] public float menu_min_width  = 180;

    // ----------------------------------------------------------------
    // Derived - built from the above, never serialised
    // ----------------------------------------------------------------
    public A_Font? font;

    public UIStyle_Text style_text     = new UIStyle_Text();
    public UIStyle_Text style_text_dim = new UIStyle_Text();

    public UIStyle_Rect style_rect      = new UIStyle_Rect(); //default surface
    public UIStyle_Rect style_panel     = new UIStyle_Rect(); //page background
    public UIStyle_Rect style_panel_alt = new UIStyle_Rect(); //raised: popups, tab strip

    public UIStyle_Button style_button          = new UIStyle_Button();
    public UIStyle_Rect   style_button_normal   = new UIStyle_Rect();
    public UIStyle_Rect   style_button_hovered  = new UIStyle_Rect();
    public UIStyle_Rect   style_button_pressed  = new UIStyle_Rect();
    public UIStyle_Rect   style_button_disabled = new UIStyle_Rect();

    public ImpUITheme()
    {
        Build();
    }

    public override void OnLoaded()
    {
        Build();
    }

    public override string File_GetExtension()
    {
        return "ImpTheme";
    }

    // Rebuilds every derived style from the palette. Safe to call again after poking
    // palette values at runtime.
    public void Build()
    {
        font = font_file == "" ? null : Load<A_Font>(font_file);

        style_text = new UIStyle_Text
        {
            font = font, size = font_size, spacing = font_spacing, color = col_text,
        };
        style_text_dim = new UIStyle_Text
        {
            font = font, size = font_size, spacing = font_spacing, color = col_text_dim,
        };

        style_rect      = new UIStyle_Rect { color = col_panel };
        style_panel     = new UIStyle_Rect { color = col_background };
        style_panel_alt = new UIStyle_Rect { color = col_panel_alt };

        style_button_normal   = new UIStyle_Rect { color = col_panel_alt };
        style_button_hovered  = new UIStyle_Rect { color = col_hover };
        style_button_pressed  = new UIStyle_Rect { color = col_pressed };
        style_button_disabled = new UIStyle_Rect { color = col_disabled };

        style_button = new UIStyle_Button
        {
            rect_normal   = style_button_normal,
            rect_hovered  = style_button_hovered,
            rect_pressed  = style_button_pressed,
            rect_disabled = style_button_disabled,
            use_blend_time = true,
            blend_time = 0.1f,
        };
    }
}
