using System.Numerics;
using Engine.Assets;
using Engine.Comps._1D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._3D;

public class C3_Character : C3_Collider
{
    public C3_Character()
    {
        type = EColliderType.Capsule;
        move_mode=A_MoveMode.DEFAULT;
        physics_enabled=true;
        movement_enabled=true;
        collision_preset = A_CollisionPreset.PRESET_CHARACTER;
        mesh.physics_enabled = false;

        mesh.transform.rotation.Z = -90;
        Child_Add(mesh, true);
        Child_Add(skeleton, true);
        Child_Add(creature, true);
    }
    
    // ==========================================================================================
    // Components
    // ==========================================================================================
    
    //3D
    public C3_Mesh mesh = new()
    {
        mesh = A_Mesh.MANNEQUIN
    };
    public C3_Skeleton skeleton = new()
    {

    };
    
    //2D
    public C1_Creature creature = new()
    {

    };

    public override void Input_Down(ImpPlayer player, TLabel ia, Vector3 axis, double dt)
    {
        base.Input_Down(player, ia, axis, dt);
        if (ia == "_Move")
        {
            Phys_MoveByRot(axis,player.control_rotation);
        }
    }
}