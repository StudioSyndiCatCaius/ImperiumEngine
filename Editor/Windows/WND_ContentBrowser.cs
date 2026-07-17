using ImperiumEngine.Objects._2D.Trees;
using ImperiumCore.Enums;

namespace ImperiumEditor.Windows;

public class wnd_ContentBrowser : EditorWindow
{
    public O2D_Tree_FileExplorer ui_explorer;

    public wnd_ContentBrowser()
    {
        Anchors_Preset(EAnchorPreset.FullRect);

        ui_explorer = new O2D_Tree_FileExplorer();
        ui_explorer.Anchors_Preset(EAnchorPreset.FullRect);
        Child_Add(ui_explorer);
    }
}
