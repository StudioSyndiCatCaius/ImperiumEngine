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
        transform={rotation = new(0, 0, 0)}
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

    public override void OnBegin()
    {
        Input_SetOwnerActive(0, true);
        base.OnBegin();
    }

    public override void Input_Update(ImpPlayer player, TLabel iaction, double dt, Vector3 axis)
    {
        base.Input_Update(player, iaction, dt, axis);
        if (iaction != "_Move")
        {
            return;
        }

        if (ImpApp.view_target!=null)
        {
            
            Vector3 local = new(axis.Z, axis.Y, -axis.X);
            Phys_MoveByRot(local, 1,ImpApp.view_target.cached_global_transform.rotation);
        }
        
      
    }

    public override void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        base.Input_Pressed(player, iaction, axis);
        if (iaction != "_Jump")
        {
            return;
        }
        if (!is_grounded)
        {
            return;
        }
        A_MoveMode mode = move_mode;
        if (mode == null)
        {
            mode = A_MoveMode.DEFAULT;
        }
        Phys_Launch(Vector3.UnitY, mode.jump_speed, false, true);
    }

    static C3_Camera Camera_Find(ImpComp n)
    {
        if (n is C3_Camera cam)
        {
            return cam;
        }
        for (int i = 0; i < n.children.Count; i++)
        {
            C3_Camera found = Camera_Find(n.children[i]);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
