using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Objects._3D;

//Transit between 2 points on a Level, OR between 2 Levels
public class C3_Transit : ImpComponent3D
{
    public C3_Transit linked_transit;
    
    public bool is_level_transit;
    public A_Level linked_level;
    
}