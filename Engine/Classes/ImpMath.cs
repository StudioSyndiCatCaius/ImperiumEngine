using System.Numerics;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Classes;

// rotation conversions matching how components build their rotation:
// Quaternion.CreateFromYawPitchRoll(Y, X, Z) with degrees stored in transform.Rotation
public static class ImpMath
{
    // ------------------------------------------------------------------------
    // double
    // ------------------------------------------------------------------------
    public static double D_Interp(double current, double target, double dt, bool constant = false)
    {
        if (constant) return target;
        return current + (target - current) * dt;
        
    }
    
    
    // ------------------------------------------------------------------------
    // int
    // ------------------------------------------------------------------------
    public static double I_Interp(int current, int target, double dt, bool constant = false)
    {
        if (constant) return target;
        return current + (target - current) * dt;
    }
    
    // ------------------------------------------------------------------------
    // Vector 2
    // ------------------------------------------------------------------------
    public static double V2_Interp(Vector2 current, Vector2 target, double dt, bool constant = false)
    {
        if (constant) return target.X;
        return current.X + (target.X - current.X) * dt;
    }
    
    // ------------------------------------------------------------------------
    // Vector 3
    // ------------------------------------------------------------------------
    public static double V3_Interp(Vector3 current, Vector3 target, double dt, bool constant = false)
    {
        if (constant) return target.X;
        return current.X + (target.X - current.X) * dt;
    }
    
    // ------------------------------------------------------------------------
    // Transform 2
    // ------------------------------------------------------------------------
    public static TTransform2D T2_Interp(TTransform2D current, TTransform2D target, double dt, bool constant = false)
    {
        if (constant) return target;
        return current;
    }
    
    // ------------------------------------------------------------------------
    // Transform 3
    // ------------------------------------------------------------------------
    public static TTransform3D T3_Interp(TTransform3D current, TTransform3D target, double dt, bool constant = false)
    {
        if (constant) return target;
        return current;
    }
    
    // ------------------------------------------------------------------------
    // Quaternion
    // ------------------------------------------------------------------------
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