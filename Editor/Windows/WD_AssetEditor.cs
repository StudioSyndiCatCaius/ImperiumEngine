using ImperiumEngine.Comps._2D;

namespace Editor.Windows;

public class WD_AssetEditor : EdWindow
{
    public WD_AssetEditor()
    {
        name = "Assets";
        Child_Add(new C2_Text("Asset Editor"));
    }
}