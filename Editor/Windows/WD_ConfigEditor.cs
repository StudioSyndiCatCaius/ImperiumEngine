using ImperiumEngine.Comps._2D;

namespace Editor.Windows;

public class WD_ConfigEditor : EdWindow
{
    public WD_ConfigEditor()
    {
        name = "Editor Config";
        Child_Add(new C2_Text("Editor Config"));
    }
}