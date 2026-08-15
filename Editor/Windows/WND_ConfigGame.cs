using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;

using ImperiumEngine.Structs;
namespace Editor.Windows;

public class WND_ConfigGame : EdWindow
{
    C2_Inspector inspector=new ()
    {
        layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
        };
    
    public WND_ConfigGame()
    {
        name = "Config Game";
        Child_Add(inspector);
    }
}