using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;

namespace Editor.Windows;

public class WND_ConfigEditor : EdWindow
{
    C2_Inspector inspector=new ()
    {
        view_alighnment_H = EUIViewportAlignment.Fill,
        view_alighnment_V = EUIViewportAlignment.Fill,
    };
    
    public WND_ConfigEditor()
    {
        name = "Config Editor";
        //inspector.Object_Add(this,true);
        Child_Add(inspector);
    }
}