using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;

namespace Editor.Windows;

public class WND_ConfigGame : EdWindow
{
    C2_Inspector inspector=new ()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };
    
    public WND_ConfigGame()
    {
        name = "Config Game";
        Child_Add(inspector);
    }
}