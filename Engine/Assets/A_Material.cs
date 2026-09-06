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

    public TMaterialCommons()
    {
    }

    public void Apply(ref Material mat)
    {
        mat.Albedo = new AlbedoMap { Texture = Gpu(color_map), Color = color_tint };
        mat.Orm = new OrmMap
        {
            Texture = Gpu(ORM_map),
            Occlusion = occlusion_intensity,
            Roughness = roughness_intensity,
            Metalness = metal_intensity,
            Specular = specular_intensity
        };
        mat.Normal = new NormalMap { Texture = Gpu(normal_map), Scale = normal_intensity };
        mat.Emission = new EmissionMap { Texture = Gpu(emission_map), Color = emission_color, Energy = emission_intensity };
    }

    public static Texture2D Gpu(A_Texture tex)
    {
        if (tex == null) return default;
        if (tex.texture.Id == 0 && !string.IsNullOrEmpty(tex.sourcefile)) tex.Source_Reimport();
        return tex.texture;
    }
}

/*
 * Base class for all materials.
 * For subtypes:
 *  - M2 as a 2d canvas/ui materials
 *  - M3 labes as a 3d material (for meshes, decals, billboards, etc)

 */
[AssetColor(210, 90, 70)]
public abstract class A_Material : ImpAsset
{
    [ImpVar] public bool two_sided;
    
    public static int draw_stamp;

    Material _gpu;

    public Material Material_Get()
    {
        _gpu = Material_Build();
        if (two_sided) _gpu.CullMode = CullMode.None;
        return _gpu;
    }

    
    protected virtual Material Material_Build()
    {
        return R3D.GetDefaultMaterial();
    }


}
