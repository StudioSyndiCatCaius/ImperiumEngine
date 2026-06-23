using System.Drawing;
using ImperiumCore;
using ImperiumCore.Classes;

namespace ImperiumEngine.Objects._3D;

public abstract class O3D_Light : ImpComponent3D
{
    [ImpVar][Exposed] public Color LightColor;
    [ImpVar][Exposed] public float LightIntensity;
    [ImpVar][Exposed] bool CastShadows=true;
}

public abstract class O3D_Light_Point : O3D_Light
{
    [ImpVar][Exposed] public float Radius;
}

public abstract class O3D_Light_Spot : O3D_Light
{
    [ImpVar][Exposed] public float ConeAngle;
    [ImpVar][Exposed] public float Radius;
}

//Light added to the whole scene. 
public abstract class O3D_Light_Ambiant : O3D_Light
{
    
}

public abstract class O3D_Light_Directional : O3D_Light
{
    
}