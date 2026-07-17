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