using ImperiumEngine.Main;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;

namespace ImperiumEngine.Assets;

public class A_Material : ImpAsset
{
    [ImpVar][Export] public Color albedo_color = Color.White;
    [ImpVar][Export] public A_Texture? albedo_texture;

    [ImpVar][Export] public float roughness = 1.0f;
    [ImpVar][Export] public float metalness = 0.0f;

    [ImpVar][Export] public Color emission_color = Color.Black;
    [ImpVar][Export] public float emission_energy = 0.0f;

    [ImpVar][Export] public bool unlit;

    // R3D material for this asset. Built from R3D's own defaults so every field this asset
    // does not model keeps a sane value, then overridden with the ones it does. Rebuilt per
    // call rather than cached, so an inspector edit shows up on the next frame.
    public Material get_Material()
    {
        var material = R3D.GetDefaultMaterial();

        material.Albedo.Color = albedo_color;
        if (albedo_texture?.get_Texture2D() is Texture2D texture && texture.Id != 0)
        {
            material.Albedo.Texture = texture;
        }

        material.Orm.Roughness = roughness;
        material.Orm.Metalness = metalness;

        material.Emission.Color = emission_color;
        material.Emission.Energy = emission_energy;

        material.Unlit = unlit;

        return material;
    }

    public override string File_GetExtension()
    {
        return "ImpMat";
    }
}

public class A_MaterialInstance : A_Material
{
    public A_Material? parent;
}
