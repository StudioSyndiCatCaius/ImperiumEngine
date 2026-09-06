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