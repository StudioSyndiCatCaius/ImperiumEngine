using ImperiumEngine.Assets.Materials;
using Raylib_cs;

namespace ImperiumEngine.Assets;

public struct TMaterialCommons
{
    [ImpVar] public A_Texture color_map=null;
    [ImpVar] public Color color_tint=Color.White;
    [ImpVar] public float specular_intensity=1.0f;
    [ImpVar] public A_Texture ORM_map=null;
    [ImpVar] public float occlusion_intensity=1.0f;
    [ImpVar] public float roughness_intensity=0.5f;
    [ImpVar] public float metal_intensity=0.0f;
    [ImpVar] public A_Texture normal_map=null;
    [ImpVar] public float normal_intensity=1.0f;
    [ImpVar] public A_Texture emission_map=null;
    [ImpVar] public Color emission_color=Color.White;
    [ImpVar] public float emission_intensity=0.0f;
        
    public TMaterialCommons()
    {
    }
}

public abstract class A_Material : ImpAsset
{
    
}