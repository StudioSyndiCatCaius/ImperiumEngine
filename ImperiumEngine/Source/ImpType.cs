
using System.Numerics;
using Silk.NET.Maths;

namespace ImperiumEngine.Source;

public struct FTransform2D
{
    public Vector2D<float> Location;
    public float           Rotation;
    public Vector2D<float>  Scale;

    public FTransform2D()
    {
        Location = default;
        Rotation = default;
        Scale = new Vector2D<float>(1f, 1f);
    }
}

public struct FTransform3D
{
    public Vector3D<double> Location;
    public Quaternion<double> Rotation;
    public Vector3D<double> Scale;

    public FTransform3D()
    {
        Location = default;
        Rotation = default;
        Scale = new Vector3D<double>(1f, 1f,1f);
    }
}


public struct FGuid
{
    private Int32 A;
    private Int32 B;
    private Int32 C;
    private Int32 D;

    public FGuid(Int32 a,Int32 b,Int32 c,Int32 d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }
}

public struct FText
{
    private string raw_string ;
    FText(string raw)
    {
        raw_string = raw;
    }
}

public struct FGameplayTag
{
    private string raw_string ;
}

public struct FGameplayTagContainer
{
    private List<FGameplayTag> tags;
}

// =====================================================================================================================
// 3D 
// =====================================================================================================================

public struct FVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Tangent;
    public Vector2 TexCoords;
    public Vector3 Bitangent;

    public const int     MAX_BONE_INFLUENCE = 4;
    public       int[]   BoneIds;
    public       float[] Weights;
}




