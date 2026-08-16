using ImperiumEngine.Comps._1D;

namespace ImperiumEngine.Comps._3D;

public class C3_Character : C3_Collider
{
    public C3_Mesh mesh=new();
    public C3_Skeleton skeleton=new();
    
    public C1_Creature creature=new();
    
    public C3_Character()
    {
        physics_enabled=true;
    }
}