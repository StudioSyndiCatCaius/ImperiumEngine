using System.Numerics;

namespace ImperiumEngine.Assets;

// configuration for an Imp3D's movement mode
public class A_MoveMode : ImpAsset
{
    // #################################################################################
    // Class
    // #################################################################################
    
    [ImpVar][Category("Speed")] public float speed=5.0f;
    [ImpVar][Category("Speed")] public float acceleration=1.0f;
    [ImpVar][Category("Speed")] public float deceleration=1.0f;
    
    [ImpVar][Category("Air")] public float air_control=1.0f;
    [ImpVar][Category("Air")] public float air_friction=0.1f;
    
    [ImpVar][Category("Gravity")] public bool gravity_enabled=true;
    [ImpVar][Category("Gravity")] public Vector3 gravity_dir=new(0, -1.0f, 0);
    [ImpVar][Category("Gravity")] public A_Curve1 gravity_accel_curve;
    [ImpVar][Category("Gravity")] public float gravity_scale=1.0f;
    
    [ImpVar][Category("Rotation")] public bool rotate_with_movement;
    [ImpVar][Category("Rotation")] public Vector3 velocity_rotation_rate=new(0, 5.0f, 5.0f);
    [ImpVar][Category("Rotation")] public bool forward_adjust_rotation;
    
    // #################################################################################
    // STATICS
    // #################################################################################
    
    public static A_MoveMode DEFAULT => new();
    public static A_MoveMode CROUCH => new()
    {
        speed=2.0f,
        air_control=0.5f
    };
    public static A_MoveMode SPRINT => new()
    {
        speed=10.0f,
        air_control=0.5f,
    };
}