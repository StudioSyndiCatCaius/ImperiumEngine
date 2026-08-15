using System.Numerics;

namespace ImperiumEngine;

public static class ImpMath
{
    // -------------------------------------------------------------------------------------
    // Vector3
    // -------------------------------------------------------------------------------------
    public static Vector3 V3_Interp(Vector3 current, Vector3 target, double dt, double speed, bool constant)
    {
        return Vector3.Lerp(current, target, (float)Math.Pow(Math.E, -dt * speed));
    }
    public static Vector3 V3_Average(List<Vector3> points)
    {
        if (points.Count == 0)
            return Vector3.Zero;

        return points.Aggregate((a, b) => a + b) / points.Count;
    }

    public static Vector2 V3_to_V2(Vector3 v,bool spatial = false)
    {
        if(spatial) return new Vector2(v.X, v.Z);
        return new Vector2(v.X, v.Y);
    }
    
    
    // -------------------------------------------------------------------------------------
    // Vector2
    // -------------------------------------------------------------------------------------
    public static Vector2 V2_Interp(Vector2 current, Vector2 target, double dt, double speed, bool constant)
    {
        return Vector2.Lerp(current, target, (float)Math.Pow(Math.E, -dt * speed));
    }
    
    public static Vector3 V2_to_V3(Vector2 v)
    {
        return new Vector3(v.X, v.Y, 0);
    }

    // X=pitch, Y=yaw, Z=roll (degrees) — same convention as Imp3D.
    public static Quaternion EulerToQuat(Vector3 euler_deg)
    {
        float deg2rad = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(euler_deg.Y * deg2rad, euler_deg.X * deg2rad, euler_deg.Z * deg2rad);
    }

    public static Vector3 QuatToEuler(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        float sinp = 2f * (q.W * q.X - q.Z * q.Y);
        float pitch, yaw, roll;
        if (MathF.Abs(sinp) >= 1f)
            pitch = MathF.CopySign(MathF.PI / 2f, sinp);
        else
            pitch = MathF.Asin(sinp);
        yaw = MathF.Atan2(2f * (q.W * q.Y + q.Z * q.X), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        roll = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));
        float rad2deg = 180f / MathF.PI;
        return new Vector3(pitch * rad2deg, yaw * rad2deg, roll * rad2deg);
    }
}