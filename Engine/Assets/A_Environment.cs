using System.Numerics;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Assets;

public class A_Environment : ImpAsset
{
    // ====================================================================================================
    // CLASS
    // ====================================================================================================

    [Category("Ambient")][ImpVar] public Color ambient_color=new Color(88, 98, 116, 255);
    [Category("Ambient")][ImpVar] public float ambient_intensity=0.22f;

    // THE SUN. A scene gets exactly one directional light and this is it - which is why
    // C3_Light offers only Point and Spot. R3D's light registry is global, so a directional
    // light left to a comp meant any number of scenes could quietly fight over the sunlight.
    [Category("Sun")][ImpVar] public bool sun_enabled=true;
    [Category("Sun")][ImpVar] public Color sun_color=new Color(255, 244, 228, 255);
    [Category("Sun")][ImpVar] public float sun_intensity=1.35f;
    //euler degrees; the direction light travels, same convention as a comp's rotation
    [Category("Sun")][ImpVar] public Vector3 sun_rotation=new Vector3(-45f, -35f, 0f);
    [Category("Sun")][ImpVar] public float sun_specular=1.15f;
    [Category("Sun")][ImpVar] public bool sun_cast_shadows=true;

    // Equirectangular .hdr panorama used as the background, and optionally as the light
    // probe. Loaded as a cubemap rather than through A_TextureHDR's own resource, because a
    // sky is a cubemap to R3D and a flat Texture2D is no use for one.
    [Category("Sky")][ImpVar] public A_TextureHDR sky_texture;
    //let the panorama light the scene too (image-based lighting), not just sit behind it
    [Category("Sky")][ImpVar] public bool sky_lights_scene=true;
    [Category("Sky")][ImpVar] public float sky_energy=1.15f;
    [Category("Sky")][ImpVar] public float sky_blur=0.0f;
    //degrees about Y, for turning the panorama to put its sun where the scene's sun is
    [Category("Sky")][ImpVar] public float sky_rotation=0.0f;

    // Distance fog. Defaults mirror R3D's own (disabled, 1..50, density 0.05) so switching a
    // mode on is the only edit needed to see something sensible.
    [Category("Fog")][ImpVar] public EFogMode fog_mode=EFogMode.Disabled;
    [Category("Fog")][ImpVar] public Color fog_color=new Color(150, 160, 175, 255);
    [Category("Fog")][ImpVar] public float fog_start=1.0f;    //Linear only
    [Category("Fog")][ImpVar] public float fog_end=50.0f;     //Linear only
    [Category("Fog")][ImpVar] public float fog_density=0.05f; //Exp / Exp2 only
    //how much the fog tints the sky as well as the geometry in front of it
    [Category("Fog")][ImpVar] public float fog_sky_affect=0.5f;

    // Tonemapping. Filmic rather than R3D's Linear default, which is what the editor has
    // always drawn with - this only makes it editable instead of hardcoded.
    [Category("Tonemap")][ImpVar] public ETonemapMode tonemap_mode=ETonemapMode.Filmic;
    [Category("Tonemap")][ImpVar] public float tonemap_exposure=1.08f;
    [Category("Tonemap")][ImpVar] public float tonemap_white=1.0f;

    // Bloom. Mode and intensity match what was hardcoded / left at R3D's default before, so
    // exposing these does not change how an existing scene looks.
    [Category("Bloom")][ImpVar] public EBloomMode bloom_mode=EBloomMode.Mix;
    [Category("Bloom")][ImpVar] public float bloom_intensity=0.08f;
    [Category("Bloom")][ImpVar] public float bloom_levels=0.55f;
    [Category("Bloom")][ImpVar] public float bloom_threshold=0.35f;
    [Category("Bloom")][ImpVar] public float bloom_soft_threshold=0.5f;
    [Category("Bloom")][ImpVar] public float bloom_filter_radius=1.0f;

    // Screen-space ambient occlusion. Grounds geometry by darkening the creases contact
    // between surfaces makes, which flat ambient light alone cannot express.
    [Category("SSAO")] [ImpVar] public bool ssao_enabled=true;
    [Category("SSAO")] [ImpVar] public float ssao_radius=1.85f;   //world-space sampling radius
    [Category("SSAO")] [ImpVar] public float ssao_intensity=2.45f; //occlusion strength
    [Category("SSAO")] [ImpVar] public float ssao_power=1.85f;    //falloff sharpness
    [Category("SSAO")] [ImpVar] public float ssao_bias=0.022f;    //keeps surfaces from occluding themselves
    [Category("SSAO")] [ImpVar] public int ssao_samples=8;

    public override string File_GetExtension() { return "ImpEnvironment"; }

    // ====================================================================================================
    // STATIC
    // ====================================================================================================
    public static A_Environment ENVI_DAY = new()
    {
        sky_texture = A_TextureHDR.SKY_DAY_2
    };
    public static A_Environment ENVI_NIGHT = new()
    {
        sky_texture = A_TextureHDR.SKY_DAY_1
    };
    public static A_Environment ENVI_MORNING = new()
    {
        sky_texture = A_TextureHDR.SKY_DAY_2
    };
    public static A_Environment ENVI_EVENING = new()
    {
        sky_texture = A_TextureHDR.SKY_DAY_1
    };
}
