using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Objects._3D;

public class C3_Skeleton : ImpPhysic3D
{
    public A_Skeleton skeleton;
    
    public A_Animation anim_default;
    public bool anim_looping=true;
    public float anim_speed=1.0f;
}