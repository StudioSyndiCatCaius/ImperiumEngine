using ImperiumEngine.Classes;
using ImperiumEngine.Objects._3D;

namespace ImperiumEngine.Objects.Assets;

public class A_CameraConfig : ImpAsset
{
    [ImpVar] public float fov         = 60f;
    [ImpVar] public float boom_length = 0f;
    
    [ImpVar] public TSpringArmLagConfig boom_lag_position;
    [ImpVar] public TSpringArmLagConfig boom_lag_rotation;

    [ImpVar] public bool is_orthographic;
}