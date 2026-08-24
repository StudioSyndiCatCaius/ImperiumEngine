using System.Numerics;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Assets;

public class A_Environment : ImpAsset
{
    // ====================================================================================================
    // CLASS
    // ====================================================================================================

    // Fills what the sun cannot reach. Kept desaturated and comparatively bright so shadowed
    // faces read as sky-lit rather than as a blue hole - the old 88/98/116 crushed them to navy.
    [Category("Ambient")][ImpVar] public Color ambient_color=new Color(150, 162, 180, 255);
    [Category("Ambient")][ImpVar] public float ambient_intensity=0.22f;

    // THE SUN. A scene gets exactly one directional light and this is it - which is why
    // C3_Light offers only Point and Spot. R3D's light registry is global, so a directional
    // light left to a comp meant any number of scenes could quietly fight over the sunlight.
    [Category("Sun")][ImpVar] public bool sun_enabled=true;
    [Category("Sun")][ImpVar] public Color sun_color=new Color(255, 244, 228, 255);
    [Category("Sun")][ImpVar] public float sun_intensity=1.5f;
    //euler degrees; the direction light travels, same convention as a comp's rotation
    [Category("Sun")][ImpVar] public Vector3 sun_rotation=new Vector3(-45f, -35f, 0f);
    [Category("Sun")][ImpVar] public float sun_specular=1.0f;
    [Category("Sun")][ImpVar] public bool sun_cast_shadows=true;
    // PCF radius in shadow-map texels. Below ~1 the filter samples a single texel and the map's
    // own staircase survives; at 4096/50m a texel is ~1 cm, so 1.5 is a soft edge, not a blurry one.
    [Category("Sun")][ImpVar] public float sun_shadow_softness=1.5f;
    // Dir shadows fit an ortho around a camera frustum this deep (meters). Smaller = tighter texels.
    // 150 spread one 4096 map over a 150 m frustum (~7 cm texels) - that was the blocky edge.
    [Category("Sun")][ImpVar] public float sun_shadow_range=50.0f;
    // NDC depth bias, so its world cost scales with sun_shadow_range: R3D's 0.001 was ~15cm of
    // peter-panning at the old 150m far-plane. Finer texels also need less of it to avoid acne.
    [Category("Sun")][ImpVar] public float sun_shadow_depth_bias=0.00005f;
    [Category("Sun")][ImpVar] public float sun_shadow_slope_bias=0.0002f;

    // Equirectangular .hdr panorama. A_TextureHDR.Cubemap_Ensure builds the R3D cubemap
    // (and IBL probe) lazily from the source file — a flat Texture2D is not a sky.
    [Category("Sky")][ImpVar] public A_TextureHDR sky_texture;
    //let the panorama light the scene too (image-based lighting), not just sit behind it
    [Category("Sky")][ImpVar] public bool sky_lights_scene=true;
    [Category("Sky")][ImpVar] public float sky_energy=1.0f;
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
    [Category("Tonemap")][ImpVar] public ETonemapMode tonemap_mode=ETonemapMode.Aces;
    // ACES rolls the highlights off instead of clipping them the way Filmic did, so a lit white
    // floor keeps its texture - but it also darkens the midtones, hence the exposure bump.
    [Category("Tonemap")][ImpVar] public float tonemap_exposure=1.25f;
    [Category("Tonemap")][ImpVar] public float tonemap_white=1.0f;

    // Bloom. Mode and intensity match what was hardcoded / left at R3D's default before, so
    // exposing these does not change how an existing scene looks.
    [Category("Bloom")][ImpVar] public EBloomMode bloom_mode=EBloomMode.Mix;
    [Category("Bloom")][ImpVar] public float bloom_intensity=0.05f;
    [Category("Bloom")][ImpVar] public float bloom_levels=0.55f;
    // Only genuinely over-white pixels bloom. At 0.35 a sunlit diffuse floor cleared the bar and
    // veiled the whole frame, which is most of what read as "hazy" next to UE.
    [Category("Bloom")][ImpVar] public float bloom_threshold=1.0f;
    [Category("Bloom")][ImpVar] public float bloom_soft_threshold=0.5f;
    [Category("Bloom")][ImpVar] public float bloom_filter_radius=1.0f;

    // Screen-space ambient occlusion. Grounds geometry by darkening the creases contact
    // between surfaces makes, which flat ambient light alone cannot express.
    // Tuned to a contact darkening rather than a lighting effect. A ~2m radius at 2.45 intensity
    // smeared a grey halo metres out from anything sitting on the ground and buried the real
    // shadow under it; AO's job is the last few centimetres where two surfaces meet.
    [Category("SSAO")] [ImpVar] public bool ssao_enabled=true;
    [Category("SSAO")] [ImpVar] public float ssao_radius=0.5f;    //world-space sampling radius
    [Category("SSAO")] [ImpVar] public float ssao_intensity=1.0f; //occlusion strength
    [Category("SSAO")] [ImpVar] public float ssao_power=1.4f;     //falloff sharpness
    [Category("SSAO")] [ImpVar] public float ssao_bias=0.01f;     //keeps surfaces from occluding themselves
    //16 is where the sample noise stops showing through the blur at this radius. 32 is not worth the cost.
    [Category("SSAO")] [ImpVar] public int ssao_samples=16;

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
