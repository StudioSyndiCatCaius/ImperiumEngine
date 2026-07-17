using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Objects._3D;

//Transit between 2 points on a Level, OR between 2 Levels
public class O3D_Transit : ImpComponent3D
{
    public O3D_Transit linked_transit;
    
    public bool is_level_transit;
    public A_Level linked_level;
    
}