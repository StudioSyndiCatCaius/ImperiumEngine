using System.Numerics;

namespace ImperiumEngine.Structs;

public struct TTransform3D
{
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Scale= Vector3.One;

    public TTransform3D()
    {
        Position = default;
        Rotation = default;
    }
}

// rotation conversions matching how components build their rotation:
// Quaternion.CreateFromYawPitchRoll(Y, X, Z) with degrees stored in transform.Rotation
public static class ImpMath
{
    public static Quaternion EulerDegToQuat(Vector3 euler_deg)
    {
        const float d2r = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(euler_deg.Y * d2r, euler_deg.X * d2r, euler_deg.Z * d2r);
    }

    public static Vector3 QuatToEulerDeg(Quaternion q)
    {
        var m = Matrix4x4.CreateFromQuaternion(q);
        const float r2d = 180f / MathF.PI;
        float sx = -m.M32;
        float pitch = MathF.Asin(Math.Clamp(sx, -1f, 1f));
        float yaw, roll;
        if (MathF.Abs(sx) < 0.9999f)
        {
            yaw = MathF.Atan2(m.M31, m.M33);
            roll = MathF.Atan2(m.M12, m.M22);
        }
        else
        {
            // gimbal lock: fold roll into yaw
            yaw = MathF.Atan2(-m.M13, m.M11);
            roll = 0;
        }
        return new Vector3(pitch * r2d, yaw * r2d, roll * r2d);
    }
}

public struct TTransform2D
{
    public Vector2 Position;
    public float Rotation;
    public Vector2 Scale= Vector2.One;

    public TTransform2D()
    {
        Position = default;
        Rotation = default;
    }
}