using ImperiumEngine.Assets;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public abstract class C1_GameMode : ImpComp
{
    [ImpVar][Category("Player")] public TClass<Imp3D> default_pawn;
    [ImpVar][Category("Player")] public TClass<C3_Camera> default_camera;
    [ImpVar][Category("Player")] public A_CameraConfig default_camera_config;

    [ImpVar][Category("State")] public List<TClass<C1_GameSystem>> systems_preload = new();
    [ImpVar][Category("State")] public TClass<C1_GameSystem> system_load;
    [ImpVar][Category("State")] public List<TClass<C1_GameSystem>> systems_postload = new();
    [ImpVar][Category("State")] public List<TClass<C1_GameSystem>> systems_persistent = new();
    [ImpVar][Category("State")] public double persistent_system_frequency = 0.2;

    public List<C1_GameSystem> active_states = new();

    private double _time_since_persistent;
    private bool _init_load_finished;

    public override void OnBegin()
    {
        base.OnBegin();

        ImpScene live = scene;
        if (live == null && ImpGame.current != null)
        {
            live = ImpGame.current.scene;
        }
        ImpComp world = null;
        if (live != null)
        {
            world = live.root;
        }

        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            ImpPlayer ply = ImpPlayer.players[i];

            Type pawn_t = default_pawn.Get();
            if (pawn_t == null || pawn_t.IsAbstract || !typeof(Imp3D).IsAssignableFrom(pawn_t))
            {
                pawn_t = typeof(C3_Character);
            }
            Imp3D pawn = ImpComp.Create(pawn_t) as Imp3D;
            ply.pawn = pawn;
            if (pawn == null)
            {
                continue;
            }
            if (world != null)
            {
                pawn.name = ImpComp.Name_Unique(world, pawn.name);
            }

            C3_PlayerStart start = null;
            ImpGame game = ImpGame.current;
            if (game != null && game.scene_previous != null)
            {
                start = C3_PlayerStart.GetFirstOfSceneLink(new TRef<ImpScene>(game.scene_previous), i);
            }
            if (start == null)
            {
                start = C3_PlayerStart.GetFirst(live, i);
            }
            if (start != null)
            {
                pawn.Transform_Set(start.Transform_Get(true), true);
            }
            if (world != null)
            {
                world.Child_Add(pawn);
            }
            pawn.OnBegin();

            Type cam_t = default_camera.Get();
            if (cam_t == null || cam_t.IsAbstract || !typeof(C3_Camera).IsAssignableFrom(cam_t))
            {
                cam_t = typeof(C3_Camera);
            }
            C3_Camera cam = ImpComp.Create(cam_t) as C3_Camera;
            if (cam == null)
            {
                continue;
            }
            A_CameraConfig src = default_camera_config;
            if (src == null)
            {
                src = A_CameraConfig.CAM_THIRDPERSON;
            }
            A_CameraConfig copy = src.Clone() as A_CameraConfig;
            if (copy == null)
            {
                copy = new A_CameraConfig();
            }
            cam.config = copy;
            cam.physics_enabled = false;
            if (world != null)
            {
                cam.name = ImpComp.Name_Unique(world, cam.name);
                world.Child_Add(cam);
            }
            cam.Transform_Set(pawn.Transform_Get(true), true);
            cam.OnBegin();
            if (live != null)
            {
                live.starting_camera = cam;
            }
            ply.target_view = cam;
        }

        void Load_Finished()
        {
            C1_GameSystem.SetListActive(systems_postload, true);
            C1_GameSystem.SetListActive(systems_persistent, true);
            _time_since_persistent = persistent_system_frequency;
            _init_load_finished = true;
        }

        C1_GameSystem.SetListActive(systems_preload, true);
        if (system_load.Get() != null)
        {
            C1_GameSystem.Activate(system_load, Load_Finished);
        }
        else
        {
            Load_Finished();
        }
    }

    public void Persistent_TryActivate()
    {
        if (!_init_load_finished)
        {
            return;
        }
        C1_GameSystem.SetListActive(systems_persistent, true);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (!_init_load_finished)
        {
            return;
        }
        if (_time_since_persistent <= 0.0)
        {
            Persistent_TryActivate();
            _time_since_persistent = persistent_system_frequency;
            if (_time_since_persistent < 0.01)
            {
                _time_since_persistent = 0.01;
            }
        }
        else
        {
            _time_since_persistent -= dt;
        }
    }
}
