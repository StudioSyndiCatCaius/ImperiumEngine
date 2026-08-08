using ImperiumEngine.Main;

namespace ImperiumEngine.Comps._3D;

public class C3_Character : C3_Collider
{
    [ImpVar][Export] public C3_Mesh c_mesh;
    [ImpVar][Export] public C3_Skeleton c_skeleton;
    [ImpVar][Export] public C3_Image c_image;
    [ImpVar][Export] public bool enable_3D=true;
    [ImpVar][Export] public bool enable_2D=false;
}