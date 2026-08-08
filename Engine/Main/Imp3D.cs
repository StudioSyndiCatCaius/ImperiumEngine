using System.Numerics;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Main;


public class Imp3D
{
    const float DEG2RAD = MathF.PI / 180f;
    const float RAD2DEG = 180f / MathF.PI;

    [ImpConfig] public static AntiAliasingMode AA_Mode;
    [ImpConfig] public static AntiAliasingPreset AA_Preset;

    // ---------------------------------------------------
    // rotation
    // ---------------------------------------------------
    // Euler angles are degrees, applied yaw (Y) then pitch (X) then roll (Z), and forward
    // is -Z to match R3D's cameras. Every conversion here uses that one convention so a
    // round trip through a quaternion gives back the angles it started from.

    public static Quaternion Quat_FromEuler(Vector3 degrees)
    {
        return Quaternion.CreateFromYawPitchRoll(
            degrees.Y * DEG2RAD, degrees.X * DEG2RAD, degrees.Z * DEG2RAD);
    }

    public static Vector3 Euler_FromQuat(Quaternion q)
    {
        q = Quaternion.Normalize(q);

        //pitch saturates at the poles rather than wrapping, which would spin yaw wildly
        float sin_pitch = 2f * (q.W * q.X - q.Y * q.Z);
        float pitch = MathF.Abs(sin_pitch) >= 1f
            ? MathF.CopySign(MathF.PI * 0.5f, sin_pitch)
            : MathF.Asin(sin_pitch);

        float yaw  = MathF.Atan2(2f * (q.W * q.Y + q.X * q.Z), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        float roll = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));

        return new Vector3(pitch, yaw, roll) * RAD2DEG;
    }

    public static Vector3 Direction_FromEuler(Vector3 degrees)
    {
        return Vector3.Transform(-Vector3.UnitZ, Quat_FromEuler(degrees));
    }

    // ---------------------------------------------------
    // viewport
    // ---------------------------------------------------

    // R3D viewports are top-left based, same as UI rects, so this is a straight pass
    // through. It exists to keep the conversion in one place rather than leaving every
    // viewport to assume the two conventions agree.
    public static Rectangle Viewport_FromScreen(Rectangle screen_rect)
    {
        return screen_rect;
    }

    // ---------------------------------------------------
    // lights
    // ---------------------------------------------------

    // R3D keeps every light in one global registry, with no notion of which scene owns
    // which. Tracking them here lets a render session switch them all off up front; the
    // C3_Lights that actually draw in that session switch themselves back on.
    public static readonly List<Light> lights = new List<Light>();

    public static Light Light_Create(LightType type)
    {
        Light light = R3D.CreateLight(type);
        lights.Add(light);
        return light;
    }

    public static void Light_Destroy(Light light)
    {
        lights.Remove(light);
        R3D.DestroyLight(light);
    }

    public static void Lights_DisableAll()
    {
        foreach (var light in lights) { R3D.SetLightActive(light, false); }
    }

    // ---------------------------------------------------
    // debug draw
    // ---------------------------------------------------

    public static void Draw_Line(Vector3 start, Vector3 end, float thickness, Color color)
    {

    }

    public static void Draw_Box(Vector3 center, Vector3 extents, Vector3 rotation, float thickness, Color color)
    {

    }

    public static void Draw_Sphere(Vector3 center, float radius, int sections, float thickness, Color color)
    {

    }

    public static void Draw_Capsule(Vector3 center, float radius, float height, float thickness, Color color)
    {

    }

    public static void Draw_Cone(Vector3 center, Vector3 angle, float length, float radius, float thickness, Color color)
    {

    }

    public static void Draw_Arrow(Vector3 center, Vector3 angle, float length, float arrow_width, Color color)
    {

    }
}
