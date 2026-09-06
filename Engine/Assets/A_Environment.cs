using System.Numerics;
using Engine.Core;
using Engine.Globals;
using R3D_cs;
using Raylib_cs;
using Environment = R3D_cs.Environment;

namespace Engine.Assets;

public class A_Environment : ImpAsset
{
    [ImpVar][Category("Background")] public Color background_color=Color.Black;
    [ImpVar][Category("Background")] public float background_intensity=1;
    
    [ImpVar][Category("Sky")] public A_Texture? sky_texture= GAsset.Asset_Load<A_Texture>(A_Texture.PATH_SKY_1);
    [ImpVar][Category("Sky")] public R3D_cs.CubemapLayout sky_layout=R3D_cs.CubemapLayout.AutoDetect;
    [ImpVar][Category("Sky")] public float sky_blur=0;
    [ImpVar][Category("Sky")] public Vector3 sky_rotation=Vector3.Zero;
    [ImpVar][Category("Sky")] public Color sky_top_color=new(98,116,140,255);
    [ImpVar][Category("Sky")] public Color sky_horizon_color=new(165,167,171,255);
    [ImpVar][Category("Sky")] public float sky_horizon_curve=0.15f;
    [ImpVar][Category("Sky")] public float sky_energy=1;
    
    [ImpVar][Category("Sun")] public float sun_intensity=2.5f;
    [ImpVar][Category("Sun")] public Color sun_color=Color.White;
    [ImpVar][Category("Sun")] public Vector3 sun_direction=new(-0.45f,1f,-0.35f);
    [ImpVar][Category("Sun")] public float sun_size=MathF.PI/180f;
    [ImpVar][Category("Sun")] public float sun_curve=0.15f;
    
    [ImpVar][Category("Ground")] public Color ground_bottom_color=new(51,43,34,255);
    [ImpVar][Category("Ground")] public Color ground_horizon_color=new(165,167,171,255);
    [ImpVar][Category("Ground")] public float ground_horizon_curve=0.02f;
    [ImpVar][Category("Ground")] public float ground_energy=1;
    
    [ImpVar][Category("Ambience")] public float ambient_intensity=1;
    [ImpVar][Category("Ambience")] public Color ambient_color=Color.White;
    
    [ImpVar][Category("SSAO")] public bool ssao_enabled=true;
    [ImpVar][Category("SSAO")] public int ssao_sample_count=16;
    [ImpVar][Category("SSAO")] public float ssao_intensity=1.25f;
    [ImpVar][Category("SSAO")] public float ssao_power=1.5f;
    [ImpVar][Category("SSAO")] public float ssao_max_radius=0.2f;
    [ImpVar][Category("SSAO")] public float ssao_radius=1;
    [ImpVar][Category("SSAO")] public float ssao_bias=0.03f;
    
    [ImpVar][Category("SSIL")] public bool ssil_enabled=false;
    [ImpVar][Category("SSIL")] public int ssil_sample_count=16;
    [ImpVar][Category("SSIL")] public float ssil_gi_intensity=1;
    [ImpVar][Category("SSIL")] public float ssil_ao_intensity=1;
    [ImpVar][Category("SSIL")] public float ssil_ao_power=1;
    [ImpVar][Category("SSIL")] public float ssil_max_radius=0.2f;
    [ImpVar][Category("SSIL")] public float ssil_radius=4;
    [ImpVar][Category("SSIL")] public float ssil_bias=0.03f;
    
    [ImpVar][Category("SSGI")] public bool ssgi_enabled=false;
    [ImpVar][Category("SSGI")] public int ssgi_slice_count=4;
    [ImpVar][Category("SSGI")] public float ssgi_edge_fade=0.1f;
    [ImpVar][Category("SSGI")] public float ssgi_distance_falloff=1;
    [ImpVar][Category("SSGI")] public float ssgi_normal_rejection=0;
    [ImpVar][Category("SSGI")] public float ssgi_intensity=1;
    [ImpVar][Category("SSGI")] public int ssgi_denoise_steps=4;
    
    [ImpVar][Category("SSR")] public bool ssr_enabled=true;
    [ImpVar][Category("SSR")] public int ssr_max_ray_steps=32;
    [ImpVar][Category("SSR")] public int ssr_binary_steps=4;
    [ImpVar][Category("SSR")] public float ssr_step_size=0.125f;
    [ImpVar][Category("SSR")] public float ssr_thickness=0.2f;
    [ImpVar][Category("SSR")] public float ssr_max_distance=4;
    [ImpVar][Category("SSR")] public float ssr_edge_fade=0.25f;
    
    [ImpVar][Category("Fog")] public Fog fog_mode=Fog.Disabled;
    [ImpVar][Category("Fog")] public Color fog_color=Color.White;
    [ImpVar][Category("Fog")] public float fog_start=1;
    [ImpVar][Category("Fog")] public float fog_end=50;
    [ImpVar][Category("Fog")] public float fog_density=0.05f;
    [ImpVar][Category("Fog")] public float fog_sky_affect=0.5f;
    
    [ImpVar][Category("Volumetric Fog")] public bool volumetric_fog_enabled=false;
    [ImpVar][Category("Volumetric Fog")] public float volumetric_fog_scattering_density=0.01f;
    [ImpVar][Category("Volumetric Fog")] public float volumetric_fog_absortion_density=0.03f;
    [ImpVar][Category("Volumetric Fog")] public Color volumetric_fog_scattering_color=Color.White;
    [ImpVar][Category("Volumetric Fog")] public float volumetric_fog_anisotropy=0.5f;
    [ImpVar][Category("Volumetric Fog")] public Color volumetric_fog_emission_color=Color.White;
    [ImpVar][Category("Volumetric Fog")] public float volumetric_fog_emission_energy=0;
    [ImpVar][Category("Volumetric Fog")] public float volumetric_fog_sky_affect=0.5f;
    [ImpVar][Category("Volumetric Fog")] public float volumetric_fog_length=50;
    [ImpVar][Category("Volumetric Fog")] public float volumetric_fog_step_size=1;
    
    [ImpVar][Category("DoF")] public DoF dof_mode=DoF.Disabled;
    [ImpVar][Category("DoF")] public float dof_focus_point=10;
    [ImpVar][Category("DoF")] public float dof_focus_scale=1;
    [ImpVar][Category("DoF")] public float dof_near_scale=1;
    [ImpVar][Category("DoF")] public float dof_max_blur_size=20;
    
    [ImpVar][Category("Bloom")] public Bloom bloom_mode=Bloom.Disabled;
    [ImpVar][Category("Bloom")] public float bloom_levels=0.5f;
    [ImpVar][Category("Bloom")] public float bloom_intensity=0.05f;
    [ImpVar][Category("Bloom")] public float bloom_threshold=0;
    [ImpVar][Category("Bloom")] public float bloom_soft_threshold=0.5f;
    [ImpVar][Category("Bloom")] public float bloom_filter_radius=1;
    
    [ImpVar][Category("Auto Exposure")] public bool auto_exposure_enabled=false;
    [ImpVar][Category("Auto Exposure")] public float auto_exposure_min_ev=-1;
    [ImpVar][Category("Auto Exposure")] public float auto_exposure_max_ev=1;
    [ImpVar][Category("Auto Exposure")] public float auto_exposure_compensation=0;
    [ImpVar][Category("Auto Exposure")] public float auto_exposure_adapt_bright=0.5f;
    [ImpVar][Category("Auto Exposure")] public float auto_exposure_adapt_dark=1;
    
    [ImpVar][Category("Tonemap")] public Tonemap tonemap_mode=Tonemap.Linear;
    [ImpVar][Category("Tonemap")] public float tonemap_exposure=1;
    [ImpVar][Category("Tonemap")] public float tonemap_white=1;
    
    [ImpVar][Category("Color")] public float color_brightness=1;
    [ImpVar][Category("Color")] public float color_contrast=1;
    [ImpVar][Category("Color")] public float color_saturation=1;

    Cubemap _sky;
    AmbientMap _ambient;
    bool _has_sky;
    bool _has_ambient;
    static Light _sun;
    static bool _sun_ok;

    public void Refresh(bool rebuild_ibl = true)
    {
        Cubemap prev_sky = _sky;
        AmbientMap prev_amb = _ambient;
        bool prev_has_sky = _has_sky;
        bool prev_has_amb = _has_ambient;
        bool rebuilt = false;

        if (rebuild_ibl || !_has_sky)
        {
            Cubemap sky = default;
            string? sky_path = sky_texture?.sourcefile;
            if (string.IsNullOrEmpty(sky_path) && sky_texture != null)
                sky_path = sky_texture.Source_Get()?.filepath;
            if (!string.IsNullOrEmpty(sky_path))
            {
                sky_path = GFile.Make_Path_Absolute(sky_path);
                if (File.Exists(sky_path))
                    sky = R3D.LoadCubemap(sky_path, sky_layout);
            }
            if (sky.Texture == 0)
            {
                sky = R3D.GenProceduralSky(256, new ProceduralSky
                {
                    SkyTopColor = sky_top_color,
                    SkyHorizonColor = sky_horizon_color,
                    SkyHorizonCurve = sky_horizon_curve,
                    SkyEnergy = sky_energy,
                    GroundBottomColor = ground_bottom_color,
                    GroundHorizonColor = ground_horizon_color,
                    GroundHorizonCurve = ground_horizon_curve,
                    GroundEnergy = ground_energy,
                    SunDirection = sun_direction,
                    SunColor = sun_color,
                    SunSize = sun_size,
                    SunCurve = sun_curve,
                    SunEnergy = sun_intensity,
                });
            }

            AmbientMap amb = default;
            bool has_sky = sky.Texture != 0;
            bool has_amb = false;
            if (has_sky)
            {
                amb = R3D.GenAmbientMap(sky, AmbientFlags.Illumination | AmbientFlags.Reflection);
                has_amb = amb.Irradiance != 0 || amb.Prefilter != 0;
            }

            _sky = sky;
            _ambient = amb;
            _has_sky = has_sky;
            _has_ambient = has_amb;
            rebuilt = true;
        }

        Environment env = R3D.GetEnvironmentEx();
        env.Background = new EnvBackground
        {
            Color = background_color,
            Energy = background_intensity,
            SkyBlur = sky_blur,
            Sky = _sky,
            Rotation = GMath.Euler_2_Quat(sky_rotation),
        };
        env.Ambient = new EnvAmbient
        {
            Color = ambient_color,
            Energy = ambient_intensity,
            Map = _ambient,
        };
        env.Ssao = new EnvSSAO
        {
            SampleCount = ssao_sample_count,
            Intensity = ssao_intensity,
            Power = ssao_power,
            MaxRadius = ssao_max_radius,
            Radius = ssao_radius,
            Bias = ssao_bias,
            Enabled = ssao_enabled,
        };
        env.Ssil = new EnvSSIL
        {
            SampleCount = ssil_sample_count,
            GiIntensity = ssil_gi_intensity,
            AoIntensity = ssil_ao_intensity,
            AoPower = ssil_ao_power,
            MaxRadius = ssil_max_radius,
            Radius = ssil_radius,
            Bias = ssil_bias,
            Enabled = ssil_enabled,
        };
        env.Ssgi = new EnvSSGI
        {
            SliceCount = ssgi_slice_count,
            EdgeFade = ssgi_edge_fade,
            DistanceFalloff = ssgi_distance_falloff,
            NormalRejection = ssgi_normal_rejection,
            Intensity = ssgi_intensity,
            DenoiseSteps = ssgi_denoise_steps,
            Enabled = ssgi_enabled,
        };
        env.Ssr = new EnvSSR
        {
            MaxRaySteps = ssr_max_ray_steps,
            BinarySteps = ssr_binary_steps,
            StepSize = ssr_step_size,
            Thickness = ssr_thickness,
            MaxDistance = ssr_max_distance,
            EdgeFade = ssr_edge_fade,
            Enabled = ssr_enabled,
        };
        env.Fog = new EnvFog
        {
            Mode = fog_mode,
            Color = fog_color,
            Start = fog_start,
            End = fog_end,
            Density = fog_density,
            SkyAffect = fog_sky_affect,
        };
        env.VolumetricFog = new VolumetricFog
        {
            ScatteringDensity = volumetric_fog_scattering_density,
            AbsortionDensity = volumetric_fog_absortion_density,
            ScatteringColor = volumetric_fog_scattering_color,
            Anisotropy = volumetric_fog_anisotropy,
            EmissionColor = volumetric_fog_emission_color,
            EmissionEnergy = volumetric_fog_emission_energy,
            SkyAffect = volumetric_fog_sky_affect,
            Length = volumetric_fog_length,
            StepSize = volumetric_fog_step_size,
            Enabled = volumetric_fog_enabled,
        };
        env.Dof = new EnvDoF
        {
            Mode = dof_mode,
            FocusPoint = dof_focus_point,
            FocusScale = dof_focus_scale,
            NearScale = dof_near_scale,
            MaxBlurSize = dof_max_blur_size,
        };
        env.Bloom = new EnvBloom
        {
            Mode = bloom_mode,
            Levels = bloom_levels,
            Intensity = bloom_intensity,
            Threshold = bloom_threshold,
            SoftThreshold = bloom_soft_threshold,
            FilterRadius = bloom_filter_radius,
        };
        env.AutoExposure = new EnvAutoExposure
        {
            MinEV = auto_exposure_min_ev,
            MaxEV = auto_exposure_max_ev,
            ExposureCompensation = auto_exposure_compensation,
            AdaptationToBright = auto_exposure_adapt_bright,
            AdaptationToDark = auto_exposure_adapt_dark,
            Enabled = auto_exposure_enabled,
        };
        env.Tonemap = new EnvTonemap
        {
            Mode = tonemap_mode,
            Exposure = tonemap_exposure,
            White = tonemap_white,
        };
        env.Color = new EnvColor
        {
            Brightness = color_brightness,
            Contrast = color_contrast,
            Saturation = color_saturation,
        };
        R3D.SetEnvironmentEx(env);
        ApplySun();

        if (rebuilt)
        {
            if (prev_has_amb) R3D.UnloadAmbientMap(prev_amb);
            if (prev_has_sky) R3D.UnloadCubemap(prev_sky);
        }
    }

    void ApplySun()
    {
        if (!_sun_ok || !R3D.IsLightValid(_sun))
        {
            _sun = R3D.CreateLight(LightType.Dir);
            _sun_ok = R3D.IsLightValid(_sun);
            if (_sun_ok)
            {
                R3D.EnableShadow(_sun);
                R3D.SetShadowUpdateMode(_sun, Imp3D.shadow_update_mode);
            }
        }
        if (!_sun_ok) return;

        Vector3 from = sun_direction;
        if (from.LengthSquared() < 1e-10f) from = GMath.WORLD_UP;
        from = Vector3.Normalize(from);
        R3D.SetLightTarget(_sun, from * 100f, Vector3.Zero);
        R3D.SetLightColor(_sun, sun_color);
        R3D.SetLightEnergy(_sun, sun_intensity);
        if (sun_intensity > 0)
            R3D.EnableLight(_sun);
        else
            R3D.DisableLight(_sun);
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    [Builtin] public static A_Environment DEFAULT = new();
    [Builtin] public static A_Environment MORNING = new();
    [Builtin] public static A_Environment DAY = new();
    [Builtin] public static A_Environment EVENING = new();
    [Builtin] public static A_Environment NIGHT = new();
}
