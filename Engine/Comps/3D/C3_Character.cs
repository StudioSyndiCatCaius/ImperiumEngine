using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Character : C3_Collider
{
    public C3_Mesh mesh=new()
    {
        mesh = A_Mesh.SK_MANNEQUIN,
        transform={rotation = new(0, 180, 0)}
    };
    public C3_Skeleton skeleton=new();
    public C1_Creature creature=new();
    
    public C3_Character()
    {
        physics_enabled=true;
        movement_enabled=true;
        shape = ECollisionShape.Capsule;
        extents = new Vector3(0.4f, 1.8f, 0.4f);
        collision_preset = A_CollisionPreset.PRESET_PAWN;
        move_mode = A_MoveMode.DEFAULT;
        if (mesh != null)
        {
            mesh.physics_enabled = false;
        }
        
        Child_Add(mesh);
        Child_Add(skeleton);
        Child_Add(creature);
    }
}
