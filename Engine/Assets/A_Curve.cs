using Engine.Interfaces;

namespace Engine.Assets;


public struct TCurve
{
    public TCurveKey[] keys;

    public float GetValue(float time)
    {
        return 0.0f; //implement later
    }
}

public struct TCurveKey
{
    [ImpVar] public float time;
    [ImpVar] public float value;
    [ImpVar] public float inTangent;
    [ImpVar] public float outTangent;
}

public abstract class A_Curve : I_File , I_Property
{
    public virtual TCurve Curve_Get(int index)
    {
        return new TCurve();
    }
    public virtual float Curve_GetValue(float time, int index = 0)
    {
        return Curve_Get(index).GetValue(time);
    }
}


public class A_Curve1 : A_Curve
{
    [ImpVar] public TCurve curve;

    public override TCurve Curve_Get(int index=0)
    {
        return curve;
    }
    
}


public class A_Curve2 : A_Curve
{
    [ImpVar] public TCurve x;
    [ImpVar] public TCurve y;
    
    public override TCurve Curve_Get(int index)
    {
        switch (index)
        {
            case 0: return x;
            case 1: return y;
            default: return new TCurve();
        }
    }
}


public class A_Curve3 : A_Curve
{
    [ImpVar] public TCurve x;
    [ImpVar] public TCurve y;
    [ImpVar] public TCurve z;
    
    public override TCurve Curve_Get(int index)
    {
        switch (index)
        {
            case 0: return x;
            case 1: return y;
            case 2: return z;
            default: return new TCurve();
        }
    }
}

public class A_Curve4 : A_Curve
{
    [ImpVar] public bool is_color;
    [ImpVar] public TCurve x;
    [ImpVar] public TCurve y;
    [ImpVar] public TCurve z;
    [ImpVar] public TCurve a;
    
    public override TCurve Curve_Get(int index)
    {
        switch (index)
        {
            case 0: return x;
            case 1: return y;
            case 2: return z;
            case 3: return a;
            default: return new TCurve();
        }
    }
}