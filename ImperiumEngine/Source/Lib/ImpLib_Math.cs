using OpenTK.Mathematics;

namespace ImperiumEngine.Source.Cores;

public struct FTransform3D
{
    public Vector3 location;
    public Vector3 rotation;
    public Vector3 scale;

    public FTransform3D()
    {
        location = Vector3.Zero;
        rotation = Vector3.Zero;
        scale = Vector3.One;
    }
}

public struct FTransform2D
{
    public Vector2 location;
    public float rotation;
    public Vector2 scale;

    public FTransform2D()
    {
        location = Vector2.Zero;
        rotation = 0;
        scale = Vector2.One;
    }
}