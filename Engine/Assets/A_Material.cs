using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;

namespace ImperiumEngine.Assets;

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

    public static Texture2D Map_Gpu(A_Texture map, Texture2D fallback)
    {
        if (map == null || map.texture.Id == 0)
        {
            return fallback;
        }
        return map.Gpu_Bind3D();
    }

    public Material ToMaterial()
    {
        Material mat = R3D.GetDefaultMaterial();

        AlbedoMap alb = mat.Albedo;
        if (color_map != null && color_map.texture.Id != 0)
        {
            alb.Texture = color_map.Gpu_Bind3D();
        }
        alb.Color = color_tint;
        mat.Albedo = alb;

        OrmMap orm = mat.Orm;
        if (ORM_map != null && ORM_map.texture.Id != 0)
        {
            orm.Texture = ORM_map.Gpu_Bind3D();
        }
        orm.Occlusion = occlusion_intensity;
        orm.Roughness = roughness_intensity;
        orm.Metalness = metal_intensity;
        orm.Specular = specular_intensity;
        mat.Orm = orm;

        NormalMap nrm = mat.Normal;
        if (normal_map != null && normal_map.texture.Id != 0)
        {
            nrm.Texture = normal_map.Gpu_Bind3D();
        }
        nrm.Scale = normal_intensity;
        mat.Normal = nrm;

        EmissionMap emi = mat.Emission;
        if (emission_map != null && emission_map.texture.Id != 0)
        {
            emi.Texture = emission_map.Gpu_Bind3D();
        }
        emi.Color = emission_color;
        emi.Energy = emission_intensity;
        mat.Emission = emi;

        if (two_sided)
        {
            mat.CullMode = CullMode.None;
        }
        else
        {
            mat.CullMode = CullMode.Back;
        }
        return mat;
    }

    public static TMaterialCommons FromMaterial(Material mat)
    {
        Material def = R3D.GetDefaultMaterial();
        TMaterialCommons c = new TMaterialCommons();
        c.color_tint = mat.Albedo.Color;
        c.color_map = Texture_From(mat.Albedo.Texture, def.Albedo.Texture);
        c.occlusion_intensity = mat.Orm.Occlusion;
        c.roughness_intensity = mat.Orm.Roughness;
        c.metal_intensity = mat.Orm.Metalness;
        c.specular_intensity = mat.Orm.Specular;
        c.ORM_map = Texture_From(mat.Orm.Texture, def.Orm.Texture);
        c.normal_intensity = mat.Normal.Scale;
        c.normal_map = Texture_From(mat.Normal.Texture, def.Normal.Texture);
        c.emission_color = mat.Emission.Color;
        c.emission_intensity = mat.Emission.Energy;
        c.emission_map = Texture_From(mat.Emission.Texture, def.Emission.Texture);
        c.two_sided = mat.CullMode == CullMode.None;
        return c;
    }

    static A_Texture Texture_From(Texture2D tex, Texture2D def)
    {
        if (tex.Id == 0)
        {
            return null;
        }
        if (def.Id != 0 && tex.Id == def.Id)
        {
            return null;
        }
        A_Texture asset = new A_Texture();
        asset.texture = tex;
        return asset;
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

    public override string File_GetExtension()
    {
        return "ImpMaterial";
    }
}
