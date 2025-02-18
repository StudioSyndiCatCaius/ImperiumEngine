using System.Drawing;

namespace ImperiumEngine.Source.Objects._3D;



public abstract class O3D_Light : ImpComponent3D
{
    public float LightIntensity;
    public Color LightColor;
}

public class O3D_Light_Directional: O3D_Light
{
    
}

public class O3D_Light_Sky: O3D_Light
{
    
}

public class O3D_Light_Point: O3D_Light
{
    
}