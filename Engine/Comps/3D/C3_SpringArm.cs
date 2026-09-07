using System.Numerics;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;

namespace Engine.Comps._3D;

public class C3_SpringArm : Imp3D
{
    [ImpVar] public float length;
    [ImpVar] public bool lag_position = false;
    [ImpVar] public float lag_position_speed = 10;
    [ImpVar] public bool lag_rotation = false;
    [ImpVar] public float lag_rotation_speed = 10;

    public TTransform3 end_transform;
    public TTransform3 lag_transform;

    private bool lag_init = false;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!lag_init)
        {
            lag_transform = global_transform;
            lag_init = true;
        }
        else
        {
            if (lag_position)
            {
                lag_transform.position = GMath.V3_Interp(
                    lag_transform.position,
                    global_transform.position,
                    dt,
                    lag_position_speed);
            }
            else
            {
                lag_transform.position = global_transform.position;
            }

            if (lag_rotation)
            {
                lag_transform.rotation = GMath.V3_Interp(
                    lag_transform.rotation,
                    global_transform.rotation,
                    dt,
                    lag_rotation_speed);
            }
            else
            {
                lag_transform.rotation = global_transform.rotation;
            }

            lag_transform.scale = global_transform.scale;
        }

        // Positive length pulls the view backward along local -X so the pawn stays in frame.
        end_transform = TTransform3.Offset(lag_transform, new Vector3(-length, 0, 0));
    }
}