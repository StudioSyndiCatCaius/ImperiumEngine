using System.Numerics;
using ImperiumEngine.Classes;
using Raylib_cs;

namespace ImperiumEngine.Objects.Assets;

public struct TCurve
{
    List<TCurveKey> keys;
}

public struct TCurveKey
{
    float time;
    float value;
}

public abstract class A_Curve : ImpAsset
{
    TCurve[] curves;
}


public class A_Curve_Float : A_Curve
{
    public float GetValue(float time)
    {
        return 0.0f;
    }
}


public class A_Curve_Vector : A_Curve
{
    public Vector3 GetValue(float time)
    {
        return Vector3.Zero;
    }
}

public class A_Curve_Color : A_Curve
{
    
    public Color GetValue(float time)
    {
        return Color.White;
    }
}