using ImperiumEngine.Classes;
using Raylib_cs;

namespace ImperiumEngine.Objects._3D;

public class O3D_Environment : ImpComponent3D
{
    
    [Exposed] public bool fog_enabled;
    [Exposed] public Color fog_color;
    [Exposed] public float fog_density;
    [Exposed] public float fog_height_falloff;
    [Exposed] public float fog_start;
    [Exposed] public float fog_end;
}