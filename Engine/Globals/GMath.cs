using System.Numerics;

namespace Engine.Globals;

public static class GMath
{
    // -------------------------------------------------------------------------------------
    // Vector3
    // -------------------------------------------------------------------------------------
    // World basis: +X forward, +Y up, +Z left (right-handed).
    public static readonly Vector3 WORLD_UP = new(0, 1, 0);
    public static readonly Vector3 WORLD_FORWARD = new(1, 0, 0);
    public static readonly Vector3 WORLD_LEFT = new(0, 0, 1);
    
    public static readonly Vector3 NO_UP = new(1, 0, 1);
    public static readonly Vector3 NO_FORWARD = new(0, 1, 0);
    public static readonly Vector3 NO_LEFT = new(1, 0, 0);

    [ScriptCall][Category("Math")][Title("Vector3 - Interp")]
    public static Vector3 V3_Interp(Vector3 current, Vector3 target, double dt, double speed, bool constant = false)
    {
        if (speed <= 0 || dt <= 0)
            return current;

        if (constant)
        {
            Vector3 delta = target - current;
            float distance = delta.Length();
            float step = (float)(speed * dt);

            if (distance <= step || distance == 0f)
                return target;

            return current + delta / distance * step;
        }

        float alpha = 1f - (float)Math.Exp(-dt * speed);
        return Vector3.Lerp(current, target, alpha);
    }

    public static Vector3 V3_Average(List<Vector3> points)
    {
        if (points.Count == 0)
            return Vector3.Zero;

        return points.Aggregate((a, b) => a + b) / points.Count;
    }

    public static Vector3 V3_Combine(List<Vector3> vectors, bool normalize = false)
    {
        if (vectors.Count == 0)
        {
            return Vector3.Zero;
        }

        Vector3 result = Vector3.Zero;
        for (int i = 0; i < vectors.Count; i++)
        {
            result += vectors[i];
        }

        if (normalize && result.LengthSquared() > 0f)
        {
            result = Vector3.Normalize(result);
        }

        return result;
    }

    public static Vector3 V3_Offset(Vector3 vector, Vector3 offset, Vector3 rotation)
    {
        return vector + Vector3.Transform(offset, (Quaternion)Euler_2_Quat(rotation));
    }

    public static Vector2 V3_to_V2(Vector3 v,bool spatial = false)
    {
        if(spatial) return new Vector2(v.X, v.Z);
        return new Vector2(v.X, v.Y);
    }

    public static Vector3 V3_Rotate(Vector3 vector, Vector3 rotation)
    {
        return Vector3.Transform(vector, (Quaternion)Euler_2_Quat(rotation));
    }

    public static Vector3 V3_LookAt(Vector3 start, Vector3 target)
    {
        Vector3 direction = target - start;

        if (direction.LengthSquared() <= float.Epsilon)
            return Vector3.Zero;

        direction = Vector3.Normalize(direction);

        float yaw = MathF.Atan2(direction.Z, direction.X);
        float horizontalLength = MathF.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
        float pitch = MathF.Atan2(direction.Y, horizontalLength);

        float rad2deg = 180f / MathF.PI;
        return new Vector3(0f, pitch * rad2deg, yaw * rad2deg);
    }
    /*   
    public static void GetRotVectors(Vector3 rotation, out Vector3 forward, out Vector3 right, out Vector3 up, bool x=false, bool y=true, bool z=false)
    {
        
    }
   */ 
    
    // -------------------------------------------------------------------------------------
    // Vector2
    // -------------------------------------------------------------------------------------

    public static Vector2 V2_Interp(Vector2 current, Vector2 target, double dt, double speed, bool constant)
    {
        return Vector2.Lerp(current, target, (float)Math.Pow(Math.E, -dt * speed));
    }

    public static Vector3 V2_to_V3(Vector2 v, bool spatial = false)
    {
        if (spatial) return new Vector3(v.X, 0, v.Y);
        return new Vector3(v.X, v.Y, 0);
    }

    // -------------------------------------------------------------------------------------
    // Rotation
    // -------------------------------------------------------------------------------------
    
    // X=roll, Y=pitch, Z=yaw (degrees) — same convention as Imp3D.
    // Rotations compose roll -> pitch -> yaw about the Y-up basis above, so a zero rotation
    // faces WORLD_FORWARD. Positive pitch tilts the nose up, positive yaw turns left.

    public static Quaternion Euler_2_Quat(Vector3 euler_deg)
    {
        float deg2rad = MathF.PI / 180f;
        float roll = euler_deg.X * deg2rad;
        float pitch = euler_deg.Y * deg2rad;
        float yaw = -euler_deg.Z * deg2rad; // negated so +yaw is turn-left

        // Numerics' q1 * q2 applies q2 first, so this is yaw(pitch(roll(v))).
        return Quaternion.CreateFromAxisAngle(WORLD_UP, yaw)
               * Quaternion.CreateFromAxisAngle(WORLD_LEFT, pitch)
               * Quaternion.CreateFromAxisAngle(WORLD_FORWARD, roll);
    }

    public static Vector3 Quat_2_Euler(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        float rad2deg = 180f / MathF.PI;

        Vector3 fwd = Vector3.Transform(WORLD_FORWARD, q);
        Vector3 left = Vector3.Transform(WORLD_LEFT, q);
        Vector3 up = Vector3.Transform(WORLD_UP, q);

        float sin_pitch = Vector3.Dot(fwd, WORLD_UP);
        float pitch, yaw, roll;
        if (MathF.Abs(sin_pitch) >= 0.99999f)
        {
            // Straight up/down: roll and yaw share an axis, so fold everything into yaw.
            pitch = MathF.CopySign(MathF.PI / 2f, sin_pitch);
            yaw = MathF.Atan2(-Vector3.Dot(left, WORLD_FORWARD), Vector3.Dot(left, WORLD_LEFT));
            roll = 0f;
        }
        else
        {
            pitch = MathF.Asin(sin_pitch);
            yaw = MathF.Atan2(Vector3.Dot(fwd, WORLD_LEFT), Vector3.Dot(fwd, WORLD_FORWARD));
            roll = MathF.Atan2(-Vector3.Dot(left, WORLD_UP), Vector3.Dot(up, WORLD_UP));
        }
        return new Vector3(roll * rad2deg, pitch * rad2deg, yaw * rad2deg);
    }
}
