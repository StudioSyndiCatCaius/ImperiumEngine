using ImperiumEngine.Comps._2D;
using ImperiumEngine.Main;

namespace ImperiumEngine.Comps._1D;

//base class for dialogs. At runtime these take up the full screen and hog ALL input until closed (is_visible=false)
public abstract class C1_Dialog : ImpComp
{
    public C1_Dialog()
    {
        is_visible = false;
    }
    
    public C2_Rect? background;
    public C2_Rect? panel;
    
}