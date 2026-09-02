using System.Numerics;
using Engine.Core;

namespace Engine.Assets;

public enum ECameraViewMode
{
    Perspective,
    Orthographic,
}

[AssetColor(70, 180, 220)]
public class A_CameraConfig : ImpAsset
{
    [ImpVar][Category("Camera")] public double fov = 60;
    [ImpVar][Category("Camera")] public ECameraViewMode view_mode = ECameraViewMode.Perspective;
    [ImpVar][Category("Camera")] public Vector3 starting_rotation;
    
    [ImpVar][Category("Boom")] public double boom_distance;
    [ImpVar][Category("Boom")] public bool boom_uses_collision;
    [ImpVar] [Category("Boom")] public bool boom_lag_position;
    [ImpVar] [Category("Boom")] public float boom_lag_position_speed = 1;
    [ImpVar] [Category("Boom")] public bool boom_lag_rotation;
    [ImpVar] [Category("Boom")] public float boom_lag_rotation_speed = 5;
    
    [ImpVar][Category("Look")] public double look_lerp = 0.5;
    [ImpVar][Category("Look")] public double look_speed = 1;

    [ImpVar][Category("Input")] public bool enable_move = true;
    [ImpVar][Category("Input")] public bool enable_rotate_H = true;
    [ImpVar][Category("Input")] public bool enable_rotate_V = true;


}
