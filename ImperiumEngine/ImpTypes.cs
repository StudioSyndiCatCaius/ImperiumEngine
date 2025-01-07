namespace ImperiumEngine;


public class ImpAsset
{
    public Guid AssetGuid;
}

public struct FGUID
{
    private Int32 A; private Int32 B; private Int32 C; private Int32 D;
}

// ====================================================================================
// Math
// ====================================================================================
public struct FVector2D { public float X; public float Y; }

public struct FVector3D
{
    public float X; public float Y; public float Z;

    public FVector3D(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
public struct FRotator3D { public float X; public float Y; public float Z; }
public struct FVector4D { public float X; public float Y; public float Z; public float W; }
public struct FTransform2D { public FVector2D location; public float rotation; }
public struct FTransform3D { public FVector3D location; public FVector3D rotation; public FVector3D scale;
    public FTransform3D()
    {
        location = default;
        rotation = default;
        scale= new FVector3D(1, 1, 1);
    }
}