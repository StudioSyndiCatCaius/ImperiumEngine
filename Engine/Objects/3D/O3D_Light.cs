using ImperiumEngine.Classes;
using Raylib_cs;

namespace ImperiumEngine.Objects._3D;

public abstract class O3D_Light : ImpComponent3D
{
    public Color color;
    public float intensity=1.0f;
    
    public bool cast_shadows=true;
}

public class O3D_LightPoint : O3D_Light
{
    
}

public class O3D_LightDirectional : O3D_Light
{
    
}

public class O3D_LightSpot : O3D_Light
{
    
}