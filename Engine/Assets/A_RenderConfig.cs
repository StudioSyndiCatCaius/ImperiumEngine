using Engine.Core;

namespace Engine.Assets;

//config for drawing a Imp3D
public class A_RenderConfig : ImpAsset
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar][Category("Drawing")] public bool enable_distance_cull;
    [ImpVar][Category("Drawing")] public float cull_distance;
    
    [ImpVar][Category("Shadows")] public bool cast_shadows;
    [ImpVar][Category("Shadows")] public float shadow_cull_distance=100;
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [Builtin] public static A_RenderConfig STATIC = new() //always draw when not frustum/occlusion culled, but cull shadow after distance
    {
        cast_shadows = true,
    }; 
    [Builtin] public static A_RenderConfig DYNAMIC = new() //after a big distance, stop drawing
    {
        enable_distance_cull = true,
        cull_distance = 100,
    }; 
    [Builtin] public static A_RenderConfig NO_SHADOW = new(); //draw but don't cast shadows'
}