using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Script;
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
    
    // Backing scene for boot, before ImpGame.Get(0) exists. After that, `current` is
    // ImpGame.current.scene so a PIE tick can see a different scene than the editor.
    static ImpScene _boot = new();

    public static ImpScene current
    {
        get
        {
            ImpGame g = ImpGame.current;
            if (g != null && g.scene != null)
            {
                return g.scene;
            }
            return _boot;
        }
        set
        {
            _boot = value ?? new ImpScene();
            ImpGame g = ImpGame.current;
            if (g != null)
            {
                g.scene = _boot;
            }
        }
    }

    // Scene used to seed ImpGame.Get(0). Does not follow the ambient game pointer.
    public static ImpScene BootScene()
    {
        return _boot;
    }

    public static ImpScene global=new();
    
    public static bool transit_active;
    public static ImpScene transit_target;

    public bool is_running;
    
    private bool was_running;
    
    // ##############################################################################################
    // ##############################################################################################
    // Class
    // ##############################################################################################
    // ##############################################################################################
    
    ImpGame _game;
    public ImpGame game
    {
        get => _game;
        set
        {
            if (_game == value)
            {
                return;
            }
            _game = value;
            if (_root != null)
            {
                _root.game_owner = value;
            }
        }
    }

    ImpComp _root;
    public ImpComp root
    {
        get => _root;
        set
        {
            if (_root == value) return;
            if (_root != null)
            {
                _root.scene = null;
                _root.game_owner = null;
            }
            _root = value ?? new ImpComp();
            _root.Detach();
            _root.scene = this;
            _root.game_owner = _game;
        }
    }

    public ImpScene()
    {
        root = new ImpComp();
        if (script_builtin == null)
        {
            script_builtin = new A_Script();
        }
        script_builtin.parent_type = new TClass<Object>(RootType_Get());
    }

    // Class the scene's script is authored against. ImpAssets are not scriptable — the script treats
    // the scene as a custom subclass of its root comp, so the root's ImpVars / calls / events are in scope.
    public Type RootType_Get()
    {
        Type t = root_type.Get();
        if (t == null)
        {
            return typeof(ImpComp);
        }
        return t;
    }

    [ScriptOverride] public virtual void OnBegin() { }
    [ScriptOverride] public virtual void OnEnd() { }
    [ScriptOverride] public virtual void OnUpdate(double dt) { }

    [ScriptCall] public void Print(string text)
    {
        if (text == null)
        {
            text = "";
        }
        Console.WriteLine(text);
    }

    Light sun_light;
    bool sun_ready;
    Cubemap sky_cubemap;
    AmbientMap sky_ambient;
    string sky_loaded_path = "";
    
    [ImpVar] public TClass<ImpComp> root_type = new TClass<ImpComp>(typeof(ImpComp));
    [ImpVar] public C3_Camera starting_camera;

    [Category("Imp")][ImpVar] public A_Script script_override;
    [ImpVar(Hidden = true)] public A_Script script_builtin = new A_Script();

    public A_Script Script_Get()
    {
        if (script_override != null)
        {
            return script_override;
        }
        if (script_builtin == null)
        {
            script_builtin = new A_Script();
        }
        // Follows root_type: re-basing the root class re-bases the script, and this migrates
        // scenes saved back when the builtin was parented to ImpScene.
        Type root_t = RootType_Get();
        if (script_builtin.parent_type.class_name != root_t.Name)
        {
            script_builtin.parent_type = new TClass<Object>(root_t);
        }
        return script_builtin;
    }
        
    [ImpVar] public Color background_color=new Color(26, 30, 36, 255);
    [Category("Canvas")][ImpVar] public Vector2 canvas_size=new(1920, 1080);

    // Lighting, sky, fog, tonemap, bloom and SSAO settings live on the environment asset so they
    // can be authored once and shared between scenes instead of duplicated on each one.
    [Category("Environment")][ImpVar] public TRef<A_Environment> environment = new(A_Environment.ENVI_DAY);

    
    public void ApplyRenderState()
    {
        A_Environment e = environment.Get();
        if (e == null)
        {
            e = A_Environment.ENVI_DAY;
        }

        string hdr_path = "";
        A_TextureHDR sky = e.sky_texture;
        if (sky != null && sky.source_file != null && !string.IsNullOrEmpty(sky.source_file.filepath))
        {
            hdr_path = ImpFile.Path_Resolve(sky.source_file.filepath);
        }

        if (!string.Equals(hdr_path, sky_loaded_path, StringComparison.OrdinalIgnoreCase))
        {
            if (sky_cubemap.Size > 0)
            {
                R3D.UnloadCubemap(sky_cubemap);
            }
            if (sky_ambient.Irradiance != 0)
            {
                R3D.UnloadAmbientMap(sky_ambient);
            }
            sky_cubemap = default;
            sky_ambient = default;
            sky_loaded_path = hdr_path;
            if (!string.IsNullOrEmpty(hdr_path) && File.Exists(hdr_path))
            {
                sky_cubemap = R3D.LoadCubemap(hdr_path, R3D_cs.CubemapLayout.Panorama);
                if (sky_cubemap.Size > 0)
                {
                    sky_ambient = R3D.GenAmbientMap(sky_cubemap, AmbientFlags.Illumination | AmbientFlags.Reflection);
                }
            }
        }

        AmbientMap ambient_map = default;
        if (e.sky_lights_scene)
        {
            ambient_map = sky_ambient;
        }
        Quaternion sky_rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, e.sky_rotation * (MathF.PI / 180f));

        // Mutate R3D's live environment by ref. Copying GetEnvironmentEx then SetEnvironmentEx(struct)
        // does not reliably push nested Background/Fog/Bloom/SSAO fields through.
        R3D.SetEnvironmentEx((ref Environment env) =>
        {
            env.Background.Color = background_color;
            env.Background.Energy = e.sky_energy;
            env.Background.SkyBlur = e.sky_blur;
            env.Background.Sky = sky_cubemap;
            env.Background.Rotation = sky_rot;
            env.Ambient.Color = e.ambient_color;
            env.Ambient.Energy = e.ambient_intensity;
            env.Ambient.Map = ambient_map;
            env.Fog.Mode = Fog.Disabled;
            if (e.fog_mode == EFogMode.Linear)
            {
                env.Fog.Mode = Fog.Linear;
            }
            else if (e.fog_mode == EFogMode.Exp)
            {
                env.Fog.Mode = Fog.Exp;
            }
            else if (e.fog_mode == EFogMode.Exp2)
            {
                env.Fog.Mode = Fog.EXP2;
            }
            env.Fog.Color = e.fog_color;
            env.Fog.Start = e.fog_start;
            env.Fog.End = e.fog_end;
            env.Fog.Density = e.fog_density;
            env.Fog.SkyAffect = e.fog_sky_affect;
            env.Tonemap.Mode = Tonemap.Linear;
            if (e.tonemap_mode == ETonemapMode.Reinhard)
            {
                env.Tonemap.Mode = Tonemap.Reinhard;
            }
            else if (e.tonemap_mode == ETonemapMode.Filmic)
            {
                env.Tonemap.Mode = Tonemap.Filmic;
            }
            else if (e.tonemap_mode == ETonemapMode.Aces)
            {
                env.Tonemap.Mode = Tonemap.Aces;
            }
            else if (e.tonemap_mode == ETonemapMode.Agx)
            {
                env.Tonemap.Mode = Tonemap.Agx;
            }
            env.Tonemap.Exposure = e.tonemap_exposure;
            env.Tonemap.White = e.tonemap_white;
            env.Bloom.Mode = Bloom.Disabled;
            if (e.bloom_mode == EBloomMode.Mix)
            {
                env.Bloom.Mode = Bloom.Mix;
            }
            else if (e.bloom_mode == EBloomMode.Additive)
            {
                env.Bloom.Mode = Bloom.Additive;
            }
            else if (e.bloom_mode == EBloomMode.Screen)
            {
                env.Bloom.Mode = Bloom.Screen;
            }
            env.Bloom.Intensity = e.bloom_intensity;
            env.Bloom.Levels = e.bloom_levels;
            env.Bloom.Threshold = e.bloom_threshold;
            env.Bloom.SoftThreshold = e.bloom_soft_threshold;
            env.Bloom.FilterRadius = e.bloom_filter_radius;
            env.Ssao.Enabled = e.ssao_enabled;
            env.Ssao.Radius = e.ssao_radius;
            env.Ssao.Intensity = e.ssao_intensity;
            env.Ssao.Power = e.ssao_power;
            env.Ssao.Bias = e.ssao_bias;
            env.Ssao.SampleCount = e.ssao_samples;
        });

        if (!sun_ready)
        {
            sun_light = R3D.CreateLight(LightType.Dir);
            sun_ready = true;
        }

        if (e.sun_enabled)
        {
            R3D.EnableLight(sun_light);
        }
        else
        {
            R3D.DisableLight(sun_light);
        }
        R3D.SetLightColor(sun_light, e.sun_color);
        R3D.SetLightEnergy(sun_light, e.sun_intensity);
        R3D.SetLightSpecular(sun_light, e.sun_specular);
        bool want_shadow = e.sun_cast_shadows;
        if (want_shadow != R3D.IsShadowEnabled(sun_light))
        {
            if (want_shadow)
            {
                R3D.EnableShadow(sun_light);
            }
            else
            {
                R3D.DisableShadow(sun_light);
            }
        }
        float d = MathF.PI / 180f;
        Quaternion sun_q = Quaternion.CreateFromYawPitchRoll(e.sun_rotation.Y * d, e.sun_rotation.X * d, e.sun_rotation.Z * d);
        R3D.SetLightDirection(sun_light, Vector3.Transform(-Vector3.UnitZ, sun_q));
    }
    
    // Running instance of the scene's Pulse script. Self is the root comp — the script is written
    // as if the scene were a subclass of it. Only exists while the scene is running (PIE).
    public ImpScriptVM impScriptVm;

    public void RBegin()
    {
        root.OnBegin();

        ImpApp.view_target = starting_camera;

        impScriptVm = null;
        A_Script s = Script_Get();
        if (s != null && s.nodes != null && s.nodes.Count > 0)
        {
            TScriptProgram p = s.Program_Get();
            for (int i = 0; i < p.errors.Count; i++)
            {
                Console.WriteLine("[Pulse] " + p.errors[i]);
            }
            impScriptVm = new ImpScriptVM(p, root);
            // The root comp receives input, so it needs a way back to this graph.
            root.impScriptVm = impScriptVm;
            impScriptVm.Event_Run("OnBegin", null);
        }
    }

    public void REnd()
    {
        ImpApp.view_target = null;
        if (impScriptVm != null)
        {
            impScriptVm.Event_Run("OnEnd", null);
            impScriptVm = null;
        }
        if (root != null)
        {
            root.impScriptVm = null;
            root.input_owner = null;
        }
        root.OnEnd();
        if (_game != null)
        {
            _game.Phys_Dispose();
        }
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
        if (is_running && _game != null && _game.phys != null)
        {
            _game.phys.Step(dt);
        }
        if (is_running && impScriptVm != null)
        {
            impScriptVm.Update(dt);
            impScriptVm.Event_Run("OnUpdate", new object[] { dt });
        }
    }
    
    public void Draw(double dt,int draw_state)
    {
        root.Draw(dt,0,draw_state);
    }
    
    // ------------------------------------
    // File
    // ------------------------------------

    // Runtime copy for Play-in-Editor. Same vars as this asset, cloned tree, not in the
    // asset cache, so playing cannot dirty or overwrite the authored scene.
    public ImpScene PlayCopy()
    {
        ImpScene copy = Clone() as ImpScene;
        if (copy == null)
        {
            copy = new ImpScene();
        }
        ImpComp src_root = root;
        ImpComp tree = null;
        if (src_root != null)
        {
            tree = src_root.Clone();
        }
        if (tree != null)
        {
            copy.root = tree;
        }
        // Clone copies ImpComp ImpVars as the original objects. starting_camera (and any
        // other scene-level comp ref) has to land on the play tree, not the authored one.
        if (copy.starting_camera != null)
        {
            C3_Camera mapped = null;
            void Find(ImpComp a, ImpComp b)
            {
                if (mapped != null || a == null || b == null)
                {
                    return;
                }
                if (ReferenceEquals(a, starting_camera) && b is C3_Camera cam)
                {
                    mapped = cam;
                    return;
                }
                int n = a.children.Count;
                if (b.children.Count < n)
                {
                    n = b.children.Count;
                }
                for (int i = 0; i < n; i++)
                {
                    Find(a.children[i], b.children[i]);
                }
            }
            Find(src_root, tree);
            copy.starting_camera = mapped;
        }
        copy.is_running = true;
        copy.filepath = "";
        copy.is_dirty = false;
        if (script_builtin != null)
        {
            copy.script_builtin = script_builtin.Clone() as A_Script;
        }
        return copy;
    }

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

    Imp2D _drop_view;
    ImpComp _drop_ghost;

    public override void SceneDrop_Enter(Imp2D view, ImpPlayer player)
    {
        SceneDrop_Exit(view, player);
        ImpScene dest_scene = SceneOf(view);
        if (dest_scene == null)
        {
            return;
        }
        _drop_view = view;
        _drop_ghost = Instantiate();
        if (_drop_ghost == null)
        {
            return;
        }
        Overlay_Set(view, _drop_ghost);
        _drop_ghost.scene = dest_scene;
        SceneDrop_Update(view, 0, player);
    }

    public override void SceneDrop_Exit(Imp2D view, ImpPlayer player)
    {
        if (Overlay_Get(_drop_view) == _drop_ghost)
        {
            Overlay_Set(_drop_view, null);
        }
        _drop_ghost?.Destroy();
        _drop_ghost = null;
        _drop_view = null;
    }

    public override void SceneDrop_Update(Imp2D view, float dt, ImpPlayer player)
    {
        if (_drop_ghost == null || view == null || player == null)
        {
            return;
        }
        if (_drop_ghost is Imp3D g3 && view is C2_Viewport3D v3)
        {
            if (v3.Trace_World(player.cursor.position, out Vector3 pos, out _))
            {
                g3.Position_Set(pos, true);
            }
        }
        else if (_drop_ghost is Imp2D g2 && view is C2_Viewport2D v2)
        {
            g2.Position_Set(v2.Trace_World(player.cursor.position), true);
        }
    }

    public override ImpComp SceneDrop_DropOnComp(ImpComp comp, ImpPlayer player)
    {
        if (_drop_ghost == null)
        {
            return null;
        }
        ImpComp dest = comp;
        ImpScene dest_scene = SceneOf(_drop_view) ?? dest?.scene;
        if (dest != null && (dest.IsPackedForeign || dest.IsOwned))
        {
            dest = ImpComp.OutlinerHost(dest);
        }
        if (dest != null && dest.IsInstanceRoot)
        {
            dest = dest.parent;
        }
        if (dest == null || dest == _drop_ghost || _drop_ghost.IsAncestorOf(dest))
        {
            dest = dest_scene?.root;
        }
        if (dest == null)
        {
            SceneDrop_Exit(_drop_view, player);
            return null;
        }

        TTransform3? w3 = null;
        TTransform2? w2 = null;
        if (_drop_ghost is Imp3D c3)
        {
            w3 = c3.Transform_Get(true);
        }
        if (_drop_ghost is Imp2D c2)
        {
            w2 = c2.Transform_Get(true);
        }
        if (Overlay_Get(_drop_view) == _drop_ghost)
        {
            Overlay_Set(_drop_view, null);
        }

        dest.Child_Add(_drop_ghost);
        if (w3.HasValue && _drop_ghost is Imp3D a3)
        {
            a3.Transform_Set(w3.Value, true);
        }
        if (w2.HasValue && _drop_ghost is Imp2D a2)
        {
            a2.Transform_Set(w2.Value, true);
        }

        ImpUndo.Comp_Moved(_drop_ghost, default, "Drop " + GetName());
        ImpComp spawned = _drop_ghost;
        _drop_ghost = null;
        _drop_view = null;
        return spawned;
    }

    static ImpScene SceneOf(Imp2D view)
    {
        if (view is C2_Viewport3D v3)
        {
            return v3.view_scene;
        }
        if (view is C2_Viewport2D v2)
        {
            return v2.view_scene;
        }
        return null;
    }

    static ImpComp Overlay_Get(Imp2D view)
    {
        if (view is C2_Viewport3D v3)
        {
            return v3.overlay;
        }
        if (view is C2_Viewport2D v2)
        {
            return v2.overlay;
        }
        return null;
    }

    static void Overlay_Set(Imp2D view, ImpComp overlay)
    {
        if (view is C2_Viewport3D v3)
        {
            v3.overlay = overlay;
        }
        else if (view is C2_Viewport2D v2)
        {
            v2.overlay = overlay;
        }
    }
}