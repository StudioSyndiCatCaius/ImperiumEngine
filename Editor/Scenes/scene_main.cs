using ImperiumCore.Classes;
using ImperiumEditor.Windows;
using ImperiumEngine.Objects._2D;

namespace ImperiumEditor.Scenes;

public class EditorLevel_Main : ImpLevel
{
    // ------------------------------
    // main elements
    // ------------------------------
    public O2D_MenuBar menu_bar;
    
    // ------------------------------
    // windows
    // ------------------------------
    public wnd_LevelEditor level_editor;
    public List<EditorWindow> OpenWindows;
    
    
    public EditorLevel_Main()
    {
        
    }
}