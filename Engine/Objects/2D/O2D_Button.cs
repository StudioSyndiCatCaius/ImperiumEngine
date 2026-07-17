using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects._2D;

public class O2D_Button : ImpComponent2D
{
    public TText text;
    public A_Texture2D icon;
    
    public bool is_pressed;
}