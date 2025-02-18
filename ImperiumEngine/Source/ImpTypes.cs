using System.Numerics;
using Silk.NET.Maths;

namespace ImperiumEngine.Source;

// ================================================================================================================
// Math
// ================================================================================================================
public struct FTransform3D
{
    public Vector3    Location;
    public Quaternion<double> Rotation;
    public Vector3     Scale;

    public FTransform3D()
    {
        Location = default;
        Rotation = default;
        Scale=Vector3.Zero;
    }
}

public struct FTransform2D
{
    public Vector2 Location;
    public double  Rotation;
    public Vector2 Scale;

    public FTransform2D()
    {
        Location = default;
        Rotation = default;
        Scale=Vector2.One;
    }
}

// ================================================================================================================
// Tags
// ================================================================================================================
public struct FGameplayTag
{
    public string tag;
}

public struct FGameplayTagContainer
{
    public List<FGameplayTag> tags;
}

// ================================================================================================================
// Path
// ================================================================================================================

public struct FPathData
{
    public string path;
    public bool   bIsFolder;
}