using ImperiumCore.Classes;
using ImperiumCore.Enums;
using ImperiumEditor.Windows;
using ImperiumEngine.Objects._2D;

namespace ImperiumEditor.Scenes;

public class EditorLevel_Main : ImpLevel
{
    const float MenuBarHeight     = 28f;
    const float ContentBrowserHeight = 250f;

    // ------------------------------
    // main elements
    // ------------------------------
    public O2D_MenuBar           menu_bar;
    public O2D_TabContainer      ui_windowTabs;
    public wnd_ContentBrowser    content_browser;

    // ------------------------------
    // open windows
    // ------------------------------
    public List<EditorWindow> OpenWindows = new();


    public EditorLevel_Main()
    {
        // menu bar — full-width strip along the top
        menu_bar = new O2D_MenuBar();
        menu_bar.Anchors_Preset(EAnchorPreset.TopWide);
        menu_bar.offset_bottom = MenuBarHeight;

        // window tab container — fills the area between menu bar and content browser
        ui_windowTabs = new O2D_TabContainer();
        ui_windowTabs.Anchors_Preset(EAnchorPreset.FullRect);
        ui_windowTabs.offset_top    =  MenuBarHeight;
        ui_windowTabs.offset_bottom = -ContentBrowserHeight;

        // content browser — fixed-height panel along the bottom
        content_browser = new wnd_ContentBrowser();
        content_browser.Anchors_Preset(EAnchorPreset.BottomWide);
        content_browser.offset_top = -ContentBrowserHeight;

        // default window: level editor on an empty level
        var levelEditor = new WND_ObjectEditor();
        OpenWindows.Add(levelEditor);
        ui_windowTabs.Tab_Add(levelEditor, "Level Editor");

        Object_Add(menu_bar);
        Object_Add(ui_windowTabs);
        Object_Add(content_browser);
    }
}
