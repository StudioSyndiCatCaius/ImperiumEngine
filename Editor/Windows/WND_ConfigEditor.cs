using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;

using ImperiumEngine.Structs;
namespace Editor.Windows;

public class WND_ConfigEditor : EdWindow
{
    C2_Inspector inspector=new ()
    {
        layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
        };
    
    public WND_ConfigEditor()
    {
        name = "Config Editor";
        //inspector.Object_Add(this,true);
        Child_Add(inspector);
    }
}