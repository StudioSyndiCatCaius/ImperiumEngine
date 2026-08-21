using System.Numerics;

namespace ImperiumEngine.Assets;

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
    [ImpVar][Category("Camera")] public double boom_distance;
    [ImpVar][Category("Camera")] public Vector3 starting_rotation;

    [ImpVar][Category("Look")] public double look_lerp = 0.5;
    [ImpVar][Category("Look")] public double look_speed = 1;

    [ImpVar][Category("Input")] public bool enable_move = true;
    [ImpVar][Category("Input")] public bool enable_rotate_H = true;
    [ImpVar][Category("Input")] public bool enable_rotate_V = true;

    public override string File_GetExtension()
    {
        return "ImpCameraConfig";
    }

    public static A_CameraConfig CAM_THIRDPERSON = new()
    {
        fov = 70,
        view_mode = ECameraViewMode.Perspective,
        boom_distance = 4,
        starting_rotation = new Vector3(-15f, 0f, 0f),
        look_lerp = 0.5,
        look_speed = 0.2,
        enable_move = true,
        enable_rotate_H = true,
        enable_rotate_V = true,
        filepath = BuiltinPrefix + "A_CameraConfig.CAM_THIRDPERSON",
    };

    public static A_CameraConfig CAM_FIRSTPERSON = new()
    {
        fov = 90,
        view_mode = ECameraViewMode.Perspective,
        boom_distance = 0,
        starting_rotation = Vector3.Zero,
        look_lerp = 0.8,
        look_speed = 0.2,
        enable_move = true,
        enable_rotate_H = true,
        enable_rotate_V = true,
        filepath = BuiltinPrefix + "A_CameraConfig.CAM_FIRSTPERSON",
    };

    public static A_CameraConfig CAM_TOPDOWN = new()
    {
        fov = 18,
        view_mode = ECameraViewMode.Orthographic,
        boom_distance = 20,
        starting_rotation = new Vector3(-90f, 0f, 0f),
        look_lerp = 0.5,
        look_speed = 0.2,
        enable_move = true,
        enable_rotate_H = false,
        enable_rotate_V = false,
        filepath = BuiltinPrefix + "A_CameraConfig.CAM_TOPDOWN",
    };
}
