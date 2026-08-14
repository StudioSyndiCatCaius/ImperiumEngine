using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;
using Environment = R3D_cs.Environment;

namespace ImperiumEngine;

public enum EFogMode { Disabled, Linear, Exp, Exp2 }

public enum ETonemapMode { Linear, Reinhard, Filmic, Aces, Agx }

public enum EBloomMode { Disabled, Mix, Additive, Screen }

[AssetColor(70, 140, 220)]
public class ImpScene : ImpAsset
{
    // #################################################################################
    // Static
    // #################################################################################
    
    public static ImpScene current=new();
    public static ImpScene global=new();
    
    public static bool transit_active;
    public static ImpScene transit_target;

    public bool is_running;
    
    private bool was_running;
    
    
    // #################################################################################
    // Class
    // #################################################################################
    
    ImpComp _root;
    public ImpComp root
    {
        get => _root;
        set
        {
            if (_root == value) return;
            if (_root != null) _root.scene = null;
            _root = value ?? new ImpComp();
            _root.Detach();
            _root.scene = this;
        }
    }

    public ImpScene()
    {
        root = new ImpComp();
    }

    Light sun_light;
    bool sun_ready;
    Cubemap sky_cubemap;
    AmbientMap sky_ambient;
    string sky_loaded_path = "";
    
    [ImpVar] public TClass<ImpComp> root_type = new TClass<ImpComp>(typeof(ImpComp));
        
    [ImpVar] public Color background_color=new Color(26, 30, 36, 255);
    [Category("Canvas")][ImpVar] public Vector2 canvas_size=new(1920, 1080);

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
    [Category("Sky")][ImpVar] public TRef<A_TextureHDR> sky_texture;
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
    
    
    public void ApplyRenderState()
    {
        string hdr_path = "";
        A_TextureHDR? sky = sky_texture.Get();
        if (sky?.source_file != null && !string.IsNullOrEmpty(sky.source_file.filepath))
            hdr_path = ImpFile.Path_Resolve(sky.source_file.filepath);
        else if (!string.IsNullOrEmpty(sky_texture.path))
            hdr_path = ImpFile.Path_Resolve(sky_texture.path);

        if (!string.Equals(hdr_path, sky_loaded_path, StringComparison.OrdinalIgnoreCase))
        {
            if (sky_cubemap.Size > 0) R3D.UnloadCubemap(sky_cubemap);
            if (sky_ambient.Irradiance != 0) R3D.UnloadAmbientMap(sky_ambient);
            sky_cubemap = default;
            sky_ambient = default;
            sky_loaded_path = hdr_path ?? "";

            if (!string.IsNullOrEmpty(hdr_path) && File.Exists(hdr_path))
            {
                sky_cubemap = R3D.LoadCubemap(hdr_path, R3D_cs.CubemapLayout.Panorama);
                if (sky_cubemap.Size > 0)
                    sky_ambient = R3D.GenAmbientMap(sky_cubemap, AmbientFlags.Illumination | AmbientFlags.Reflection);
            }
        }

        Environment env = R3D.GetEnvironmentEx();
        env.Background.Color = background_color;
        env.Background.Energy = sky_energy;
        env.Background.SkyBlur = sky_blur;
        env.Background.Sky = sky_cubemap;
        env.Background.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, sky_rotation * (MathF.PI / 180f));
        env.Ambient.Color = ambient_color;
        env.Ambient.Energy = ambient_intensity;
        env.Ambient.Map = sky_lights_scene ? sky_ambient : default;
        env.Fog.Mode = fog_mode switch
        {
            EFogMode.Linear => Fog.Linear,
            EFogMode.Exp => Fog.Exp,
            EFogMode.Exp2 => Fog.EXP2,
            _ => Fog.Disabled,
        };
        env.Fog.Color = fog_color;
        env.Fog.Start = fog_start;
        env.Fog.End = fog_end;
        env.Fog.Density = fog_density;
        env.Fog.SkyAffect = fog_sky_affect;
        env.Tonemap.Mode = tonemap_mode switch
        {
            ETonemapMode.Reinhard => Tonemap.Reinhard,
            ETonemapMode.Filmic => Tonemap.Filmic,
            ETonemapMode.Aces => Tonemap.Aces,
            ETonemapMode.Agx => Tonemap.Agx,
            _ => Tonemap.Linear,
        };
        env.Tonemap.Exposure = tonemap_exposure;
        env.Tonemap.White = tonemap_white;
        env.Bloom.Mode = bloom_mode switch
        {
            EBloomMode.Mix => Bloom.Mix,
            EBloomMode.Additive => Bloom.Additive,
            EBloomMode.Screen => Bloom.Screen,
            _ => Bloom.Disabled,
        };
        env.Bloom.Intensity = bloom_intensity;
        env.Bloom.Levels = bloom_levels;
        env.Bloom.Threshold = bloom_threshold;
        env.Bloom.SoftThreshold = bloom_soft_threshold;
        env.Bloom.FilterRadius = bloom_filter_radius;
        env.Ssao.Enabled = ssao_enabled;
        env.Ssao.Radius = ssao_radius;
        env.Ssao.Intensity = ssao_intensity;
        env.Ssao.Power = ssao_power;
        env.Ssao.Bias = ssao_bias;
        env.Ssao.SampleCount = ssao_samples;
        R3D.SetEnvironmentEx(env);

        if (!sun_ready)
        {
            sun_light = R3D.CreateLight(LightType.Dir);
            sun_ready = true;
        }
        R3D.SetLightActive(sun_light, sun_enabled);
        R3D.SetLightColor(sun_light, sun_color);
        R3D.SetLightEnergy(sun_light, sun_intensity);
        R3D.SetLightSpecular(sun_light, sun_specular);
        bool want_shadow = sun_cast_shadows;
        if (want_shadow != R3D.IsShadowEnabled(sun_light))
        {
            if (want_shadow) R3D.EnableShadow(sun_light);
            else R3D.DisableShadow(sun_light);
        }
        float d = MathF.PI / 180f;
        Quaternion sun_q = Quaternion.CreateFromYawPitchRoll(sun_rotation.Y * d, sun_rotation.X * d, sun_rotation.Z * d);
        R3D.SetLightDirection(sun_light, Vector3.Transform(-Vector3.UnitZ, sun_q));
    }
    
    public void RBegin()
    {
        root.RuntimeBegin();
    }
    
    public void REnd()
    {
        root.RuntimeEnd();
    }
    
    public void Update(double dt)
    {
        
        if (is_running!=was_running)
        {
            was_running=is_running;
            if (is_running) { RBegin(); }
            else { REnd(); }
        }
        root.Update(dt,is_running);
    }
    
    public void Draw(double dt,int draw_state)
    {
        root.Draw(dt,0,draw_state);
    }
    
    // ------------------------------------
    // File
    // ------------------------------------

    public override string File_GetExtension() { return "ImpScene"; }

    public override string Editor_GetTypeLabel() { return "Scene"; }

    [ThreadStatic] static HashSet<string> _instantiating;

    public ImpComp Instantiate()
    {
        _instantiating ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(filepath) && !_instantiating.Add(filepath)) return null;
        try
        {
            if (root == null) return null;
            ImpComp inst = root.Clone(this);
            if (inst == null) return null;
            inst.packed = new TRef<ImpScene>(this);
            BindPackedTree(inst, inst);
            if (!string.IsNullOrEmpty(filepath)) inst.name = GetName();
            return inst;
        }
        finally
        {
            if (!string.IsNullOrEmpty(filepath)) _instantiating.Remove(filepath);
        }
    }

    public static bool IsSelfInstance(ImpComp n, ImpScene scene)
    {
        if (n == null || scene == null || !n.IsInstanceRoot) return false;
        ImpScene packed = n.packed.Get();
        if (packed != null && ReferenceEquals(packed, scene)) return true;
        string a = n.packed.path;
        string b = scene.filepath;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    public static void BindPackedTree(ImpComp owner, ImpComp n)
    {
        if (n == null || owner == null) return;
        if (n != owner && !string.IsNullOrEmpty(n.packed.path))
        {
            n.packed_from = n;
            for (int i = 0; i < n.children.Count; i++)
                BindPackedTree(n, n.children[i]);
            return;
        }
        n.packed_from = owner;
        if (n != owner) n.packed = default;
        for (int i = 0; i < n.children.Count; i++)
            BindPackedTree(owner, n.children[i]);
    }

    C2_SceneView _drop_view;
    ImpComp _drop_ghost;

    public override void SceneDrop_Enter(C2_SceneView view, ImpPlayer player)
    {
        SceneDrop_Exit(view, player);
        if (view?.scene == null) return;
        _drop_view = view;
        _drop_ghost = Instantiate();
        if (_drop_ghost == null) return;
        view.drop_preview = _drop_ghost;
        _drop_ghost.scene = view.scene;
        SceneDrop_Update(view, 0, player);
    }

    public override void SceneDrop_Exit(C2_SceneView view, ImpPlayer player)
    {
        if (view != null && view.drop_preview == _drop_ghost) view.drop_preview = null;
        _drop_ghost?.Destroy();
        _drop_ghost = null;
        _drop_view = null;
    }

    public override void SceneDrop_Update(C2_SceneView view, float dt, ImpPlayer player)
    {
        if (_drop_ghost == null || view == null) return;
        if (_drop_ghost is ImpComp3D g3)
        {
            if (view.Drop_World3(player, out Vector3 pos, out _))
                g3.Position_Set(pos, true);
        }
        else if (_drop_ghost is ImpComp2D g2)
            g2.Position_Set(view.Drop_World2(player), true);
    }

    public override void SceneDrop_DropOnComp(ImpComp comp, ImpPlayer player)
    {
        if (_drop_ghost == null) return;
        ImpComp dest = comp;
        ImpScene dest_scene = _drop_view?.scene ?? dest?.scene;
        if (dest != null && (dest.IsPackedForeign || dest.IsInstanceRoot))
            dest = dest.IsInstanceRoot ? dest.parent : dest.packed_from?.parent;
        if (dest == null || dest == _drop_ghost || _drop_ghost.IsAncestorOf(dest))
            dest = dest_scene?.root;
        if (dest == null)
        {
            SceneDrop_Exit(_drop_view, player);
            return;
        }

        TTransform3? w3 = _drop_ghost is ImpComp3D c3 ? c3.Transform_Get(true) : null;
        TTransform2? w2 = _drop_ghost is ImpComp2D c2 ? c2.Transform_Get(true) : null;
        if (_drop_view != null && _drop_view.drop_preview == _drop_ghost)
            _drop_view.drop_preview = null;

        dest.Child_Add(_drop_ghost);
        if (w3.HasValue && _drop_ghost is ImpComp3D a3) a3.Transform_Set(w3.Value, true);
        if (w2.HasValue && _drop_ghost is ImpComp2D a2) a2.Transform_Set(w2.Value, true);

        ImpUndo.Comp_Moved(_drop_ghost, default, "Drop " + GetName());
        _drop_view?.gizmo_data?.Selection_Set(new[] { _drop_ghost });
        _drop_ghost = null;
        _drop_view = null;
    }
}