using Engine.Core;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;

namespace Engine.Assets;

public struct TMaterialCommons
{
    [ImpVar] public A_Texture color_map=null;
    [ImpVar] public Color color_tint=Color.White;
    [ImpVar] public float specular_intensity=0.0f;
    [ImpVar] public A_Texture ORM_map=null;
    [ImpVar] public float occlusion_intensity=1.0f;
    [ImpVar] public float roughness_intensity=0.5f;
    [ImpVar] public float metal_intensity=0.0f;
    [ImpVar] public A_Texture normal_map=null;
    [ImpVar] public float normal_intensity=1.0f;
    [ImpVar] public A_Texture emission_map=null;
    [ImpVar] public Color emission_color=Color.White;
    [ImpVar] public float emission_intensity=0.0f;
    [ImpVar] public bool two_sided;

    public TMaterialCommons()
    {
    }

}

[AssetColor(210, 90, 70)]
public abstract class A_Material : ImpAsset
{
    public static int draw_stamp;

    int _built_stamp = -1;
    Material _gpu;

    public Material Material_Get()
    {
        if (_built_stamp != draw_stamp)
        {
            _gpu = Material_Build();
            _built_stamp = draw_stamp;
        }
        return _gpu;
    }

    protected virtual Material Material_Build()
    {
        return R3D.GetDefaultMaterial();
    }


}
