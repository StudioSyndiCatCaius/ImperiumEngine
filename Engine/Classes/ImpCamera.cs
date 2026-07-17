using System.Numerics;

namespace ImperiumEngine.Classes;

public class ImpCamera
{
    public Vector3   Position;
    public Quaternion Rotation  = Quaternion.Identity;
    public float     FOV       = 90;
    public float     NearPlane = 0.1f;
    public float     FarPlane  = 1_000_000f;
}
