using System.Numerics;

namespace ImperiumCore.Structs;

public struct TTransform3D
{
    [ImpVar][Exposed] public Vector3 position;
    [ImpVar][Exposed] public Quaternion rotation;
    [ImpVar][Exposed] public Vector3 scale=Vector3.One;

    public TTransform3D()
    {
        position = default;
        rotation = default;
    }
}


public struct TTransform2D
{
    [ImpVar][Exposed] public Vector2 position;
    [ImpVar][Exposed] public double rotation;
    [ImpVar][Exposed] public Vector2 scale=Vector2.One;

    public TTransform2D()
    {
        position = default;
        rotation = 0;
    }
}

public struct TMargin2D
{
    [ImpVar][Exposed] public double left, top, right, bottom;

    public TMargin2D(double left, double top, double right, double bottom)
    {
        this.left = left; this.top = top; this.right = right; this.bottom = bottom;
    }

    public TMargin2D(double all) { left = top = right = bottom = all; }
}

public struct TVector3i
{
    [ImpVar][Exposed] public  int x, y, z;
}

public struct TVector2i
{
    [ImpVar][Exposed] public  int x, y;

    public TVector2i(int x, int y) { this.x = x; this.y = y; }

    public static TVector2i From(Vector2 v) => new((int)v.X, (int)v.Y);
}

public struct TVector2b
{
    [ImpVar][Exposed] public byte x, y;
    
}




public struct TRect2
{
    public Vector2 position;
    public Vector2 size;

    public TRect2(Vector2 position, Vector2 size) { this.position = position; this.size = size; }
    public TRect2(float x, float y, float w, float h) { position = new(x, y); size = new(w, h); }

    public float Left => position.X;
    public float Top => position.Y;
    public float Right => position.X + size.X;
    public float Bottom => position.Y + size.Y;

    public bool Contains(Vector2 p) =>
        p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;
}