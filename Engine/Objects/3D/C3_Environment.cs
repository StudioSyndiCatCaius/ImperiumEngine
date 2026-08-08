using System.Numerics;
using ImperiumEngine.Classes;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Objects._3D;

public class C3_Environment : ImpComponent3D
{
    // -----------------------------------------------------------------
    // Fog
    // -----------------------------------------------------------------
    [ImpVar] public bool  fog_enabled       = true;
    [ImpVar] public Color fog_color         = new Color(180, 190, 210, 255);
    [ImpVar] public float fog_density       = 0.01f;
    [ImpVar] public float fog_height_falloff;
    [ImpVar] public float fog_start         = 20f;
    [ImpVar] public float fog_end           = 200f;

    // -----------------------------------------------------------------
    // Sun — sun_direction points from the scene toward the sun.
    // -----------------------------------------------------------------
    [ImpVar] public Vector3 sun_direction   = Vector3.Normalize(new Vector3(0.5f, 0.8f, 0.3f));
    [ImpVar] public Color   sun_color       = new Color(255, 242, 204, 255);
    [ImpVar] public float   sun_energy      = 2.0f;
    [ImpVar] public bool    sun_shadows     = true;

    // -----------------------------------------------------------------
    // Ambient
    // -----------------------------------------------------------------
    [ImpVar] public Color  ambient_color    = new Color(13, 13, 20, 255);
    [ImpVar] public float  ambient_energy   = 0.3f;

    // -----------------------------------------------------------------
    // SSAO
    // -----------------------------------------------------------------
    [ImpVar] public bool  ssao_enabled      = true;
    [ImpVar] public float ssao_intensity    = 1.0f;
    [ImpVar] public float ssao_radius       = 1.0f; // sampling radius, world units
    [ImpVar] public float ssao_power        = 1.0f; // exponential falloff, sharper darkening

    // -----------------------------------------------------------------
    // Sky — equirectangular HDR panorama used as skybox + image-based
    // ambient light/reflections. Empty = flat background color.
    // -----------------------------------------------------------------
    [ImpVar] public string sky_hdri         = "";

    protected override bool IsSingleton() => true;

    Light       _sunLight;
    Cubemap?    _sky;
    AmbientMap? _skyAmbient;

    public C3_Environment()
    {
        _sunLight = R3D.CreateLight(LightType.Dir);
    }

    // Construction (editor + pre-Begin): load sky / push fog+sun into R3D so the viewport
    // matches without any play-session lifecycle.
    public override void OnInit()
    {
        base.OnInit();
        ReleaseSky();
        LoadSky();
        ApplyEnvironment();
        ApplySun();
    }

    public override void OnDeinit()
    {
        ReleaseEnv();
        base.OnDeinit();
    }

    // Runtime end of a play instance — same resource release as editor Deinit.
    public override void OnEnd()
    {
        ReleaseEnv();
        base.OnEnd();
    }

    void LoadSky()
    {
        if (string.IsNullOrEmpty(sky_hdri)) return;

        string path = ImpFile.Path_ToAbsolute(sky_hdri);
        if (!File.Exists(path))
        {
            Console.WriteLine($"[C3_Environment] Sky HDRI not found: {path}");
            return;
        }

        _sky        = R3D.LoadCubemap(path, R3D_cs.CubemapLayout.Panorama);
        _skyAmbient = R3D.GenAmbientMap(_sky.Value, AmbientFlags.Illumination | AmbientFlags.Reflection);
    }

    void ApplyEnvironment()
    {
        R3D.SetEnvironmentEx((ref R3D_cs.Environment env) =>
        {
            env.Ambient.Color  = ambient_color;
            env.Ambient.Energy = ambient_energy;
            env.Fog.Mode       = fog_enabled ? Fog.Exp : Fog.Disabled;
            env.Fog.Color      = fog_color;
            env.Fog.Density    = fog_density;
            env.Fog.Start      = fog_start;
            env.Fog.End        = fog_end;

            env.Ssao.Enabled   = ssao_enabled;
            env.Ssao.Intensity = ssao_intensity;
            env.Ssao.Radius    = ssao_radius;
            env.Ssao.Power     = ssao_power;

            if (_sky        is Cubemap sky)    env.Background.Sky = sky;
            if (_skyAmbient is AmbientMap map) env.Ambient.Map    = map;
        });
    }

    void ApplySun()
    {
        // Directional lights ignore position/range/attenuation — only direction matters.
        R3D.SetLightDirection(_sunLight, -Vector3.Normalize(sun_direction));
        R3D.SetLightColor(_sunLight, sun_color);
        R3D.SetLightEnergy(_sunLight, sun_energy);
        R3D.SetLightActive(_sunLight, is_visible);

        if (sun_shadows) R3D.EnableShadow(_sunLight);
        else             R3D.DisableShadow(_sunLight);
    }

    void ReleaseEnv()
    {
        R3D.SetLightActive(_sunLight, false);
        ReleaseSky();
    }

    void ReleaseSky()
    {
        if (_skyAmbient is AmbientMap map) R3D.UnloadAmbientMap(map);
        if (_sky        is Cubemap sky)    R3D.UnloadCubemap(sky);
        _skyAmbient = null;
        _sky        = null;
    }
}
