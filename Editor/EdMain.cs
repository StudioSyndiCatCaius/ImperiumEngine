using System.Numerics;
using Editor.Windows;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Main;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor;

// Root of the editor shell: a menu bar pinned to the top, a tab box filling the rest,
// and floating panels layered over both.
public class EdMain : ImpComp2D
{
    public C2_MenuBar menu_bar;
    public C2_TabBox tab_box;
    public C2_Rect panel_settings;

    public EdMain()
    {
        name = "EdMain";
        anchor_preset = EUIAnchorPreset.Full;
        cursor_filter = ECursorFilter.Pass; //pure layout container, never takes the cursor itself

        //root of the editor's theme cascade - everything below inherits this
        theme = ImpUITheme.editor_theme;

        // ------------------------------------
        // SETUP - Settings panel
        // ------------------------------------
        panel_settings = new C2_Rect
        {
            name = "Settings",
            anchor_preset = EUIAnchorPreset.Center,
            size = new Vector2(420, 240),
            is_visible = false,
            //no style: colour follows the theme. Margins are set on the comp instead, so
            //insetting the children doesn't require owning a style that ignores the theme.
            margins_inner = new TMargins { left = 14, right = 14, top = 14, bottom = 14 },
        };

        panel_settings.Child_Add(new C2_Text("Editor Settings")
        {
            name = "Title",
            anchor_preset = EUIAnchorPreset.WideTop,
        });

        panel_settings.Child_Add(new C2_Button
        {
            name = "Close",
            text = "Close",
            anchor_preset = EUIAnchorPreset.BottomRight,
            size = new Vector2(90, 26),
            on_click = _ => panel_settings.is_visible = false,
        });

        // ------------------------------------
        // SETUP - Menu Bar
        // ------------------------------------
        menu_bar = new C2_MenuBar([
            new TMenuBarOption("File", [
                new TMenuBarSubption("New", () => { }),
                new TMenuBarSubption(true),
                new TMenuBarSubption("Save Scene", () => { }),
                new TMenuBarSubption("Save All", () => { }),
            ]),
            new TMenuBarOption("Edit", [
                new TMenuBarSubption("Undo", () => { }),
                new TMenuBarSubption("Redo", () => { }),
            ]),
            new TMenuBarOption("Window", [
                new TMenuBarSubption("Settings", () => panel_settings.is_visible = !panel_settings.is_visible),
                new TMenuBarSubption(true),
                new TMenuBarSubption("Theme: Dark", () => Theme_Set(ImpUITheme.path_dark)),
                new TMenuBarSubption("Theme: Light", () => Theme_Set(ImpUITheme.path_light)),
            ]),
        ]);

        // ------------------------------------
        // SETUP - Main Tabs
        // ------------------------------------
        tab_box = new C2_TabBox
        {
            name = "Tabs",
            anchor_preset = EUIAnchorPreset.Full,
        };

        tab_box.Child_Add(new WD_SceneEditor());
        tab_box.Child_Add(new WD_AssetEditor());
        tab_box.Child_Add(new WD_ConfigGame());
        tab_box.Child_Add(new WD_ConfigEditor());

        Child_Add(menu_bar);
        Child_Add(tab_box);
        Child_Add(panel_settings);
    }

    // Swaps the editor's theme. Because the whole editor cascades from this comp's
    // `theme`, reassigning it here restyles every descendant on the next frame.
    void Theme_Set(string path)
    {
        var loaded = ImpUITheme.Load<ImpUITheme>(path);
        if (loaded == null) return;

        ImpUITheme.editor_theme = loaded;
        theme = loaded;
    }

    // A scrolling list of buttons: stands in for real config UI, and keeps the container
    // and input paths exercised by something you can actually click.
    static C2_ScrollBox Tab_BuildConfig()
    {
        var scroll = new C2_ScrollBox
        {
            name = "Config",
            anchor_preset = EUIAnchorPreset.Full,
        };

        var list = new C2_List
        {
            name = "ConfigList",
            Alignment = EUIAlignment.Vertical,
            separation = 6f,
        };

        for (int i = 1; i <= 40; i++)
        {
            int n = i;
            list.Child_Add(new C2_Button
            {
                name = $"Setting{n}",
                text = $"Setting {n}",
                size = new Vector2(0, 30),
                on_click = b => Console.WriteLine($"clicked {b.text}"),
            });
        }

        scroll.Child_Add(list);
        return scroll;
    }

    // Menu bar takes the top strip; the tab box fills everything below it; panels float
    // over the whole area. Added last, so they draw on top and win hit-testing.
    protected override void Layout_Children(Rectangle content)
    {
        menu_bar.OnLayout(content);

        float top = menu_bar.rect.Height;
        var below = new Rectangle(
            content.X, content.Y + top,
            content.Width, MathF.Max(0, content.Height - top));

        tab_box.OnLayout(below);

        if (panel_settings.is_visible) panel_settings.OnLayout(content);
    }
}
