using ImperiumCore.Classes.Components;
using ImperiumEngine.Objects._1D;
using ImperiumEngine.Objects._3D.Prim;

namespace ImperiumEngine.Objects._3D;


public class O3D_Character : O3D_Collider
{
    public O3D_Mesh bodyMesh;
    public O3D_Skeleton bodySkeleton;
    
    public O1D_Creature creature;

    public O3D_Character()
    {
        //colliderShape == EColliderShape.Capsule;
        bodySkeleton.boundMeshes.Add(bodyMesh);
    }
}