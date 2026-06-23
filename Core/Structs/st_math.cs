using System.Numerics;

namespace ImperiumCore.Structs;

public struct TTransform3D
{
    Vector3 position;
    Quaternion rotation;
    Vector3 scale=Vector3.One;

    public TTransform3D()
    {
        position = default;
        rotation = default;
    }
}


public struct TTransform2D
{
    Vector2 position;
    double rotation;
    Vector2 scale=Vector2.One;

    public TTransform2D()
    {
        position = default;
        rotation = 0;
    }
}

public struct TMargin2D
{
    double left, top, right, bottom;
}

public struct TVector3i
{
    int x, y, z;
}

public struct TVector2i
{
    int x, y;
}