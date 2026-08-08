using ImperiumEngine.Assets;
using R3D_cs;
using Raylib_cs;
using Environment = R3D_cs.Environment;

namespace ImperiumEngine.Main;

public class ImpScene : ImpAsset
{
    // ================================================================================================
    // Statics
    // ================================================================================================
    
    public static ImpScene current=new ImpScene();
    public static ImpScene global=new ImpScene();
    
    // ================================================================================================
    // Class
    // ================================================================================================
    public bool is_runtime=false;

    public Environment environment;
    bool environment_seeded;

    public ImpComp root=new ImpComp();

    [ImpVar] public A_GameMode? override_game_mode;
    [ImpVar] public Color background_color=new Color(26, 30, 36, 255);

    [ImpVar] public Color ambient_color=new Color(120, 132, 150, 255);
    [ImpVar] public float ambient_intensity=0.35f;
    
    [ImpVar] public float bloom_intensity=0.5f;

    // Screen-space ambient occlusion. Grounds geometry by darkening the creases contact
    // between surfaces makes, which flat ambient light alone cannot express.
    [Category("SSAO")] [ImpVar] public bool ssao_enabled=true;
    [Category("SSAO")] [ImpVar] public float ssao_radius=1f;      //world-space sampling radius
    [Category("SSAO")] [ImpVar] public float ssao_intensity=1f;   //occlusion strength
    [Category("SSAO")] [ImpVar] public float ssao_power=1f;       //falloff sharpness
    [Category("SSAO")] [ImpVar] public float ssao_bias=0.05f;     //keeps surfaces from occluding themselves
    [Category("SSAO")] [ImpVar] public int ssao_samples=16;

    public Action<ImpScene,ImpComp> on_comp_added;
    public Action<ImpScene,ImpComp> on_comp_removed;
    
    // Editor + Runtime -----------------
    public virtual void OnInit() { root.OnInit(); }
    public virtual void OnDeinit() { root.OnDeinit(); }
    
    // Resolves screen rectangles for the whole tree. Must run before Draw, and before
    // input hit-testing, since both read the rects it computes.
    public virtual void Layout(Rectangle area) { root.OnLayout(area); }
    public virtual void Draw(double dt, EDrawFlags flags) { root.OnDraw(dt, flags); }

    // Pushes this scene's lighting and background into R3D, which keeps them as global
    // state. A viewport calls this immediately before opening its render session, so two
    // scenes on screen at once each render under their own environment.
    public void Environment_Apply()
    {
        // Seeded from R3D's own defaults the first time round, so every knob this scene
        // does not model (tonemap, bloom, SSAO...) starts at something sensible instead of
        // the zeroes a default-constructed struct would carry.
        if (!environment_seeded)
        {
            environment = R3D.GetEnvironmentEx();
            environment_seeded = true;
        }
        environment.Tonemap.Mode = Tonemap.Filmic;
        environment.Bloom.Mode = R3D_cs.Bloom.Mix;
        //environment.Bloom.Levels = 1.0f;

        
        environment.Background.Color = background_color;
        environment.Ambient.Color = ambient_color;
        environment.Ambient.Energy = ambient_intensity;
        
        environment.Ssao.Enabled = ssao_enabled;
        environment.Ssao.Radius = ssao_radius;
        environment.Ssao.Intensity = ssao_intensity;
        environment.Ssao.Power = ssao_power;
        environment.Ssao.Bias = ssao_bias;
        environment.Ssao.SampleCount = ssao_samples;

        //environment.Bloom.Intensity=bloom_intensity;
        //environment.Bloom.Mode = Bloom.Additive;
        
        R3D.SetEnvironmentEx(environment);
        
    }


    // RUNTIME ---------------------------------
    public void Begin()
    {
        root.owning_scene = this;
        root.OnBegin();
    }
    public void End() { root.OnEnd(); }
    public void Update(double dt) { root.OnUpdate(dt); }
    
    // ------------------------------------------------
    // File
    // ------------------------------------------------
    public override string File_GetExtension()
    {
        return "ImpScene";
    }
}