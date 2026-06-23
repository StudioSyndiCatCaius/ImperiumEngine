using ImperiumCore.Classes;

namespace ImperiumEngine.Objects._2D;

public class O2D_Button : ImpComponent2D
{
    public string text;
    
    public Action<O2D_Button> OnPressed;
    public Action<O2D_Button> OnReleased;
    
}