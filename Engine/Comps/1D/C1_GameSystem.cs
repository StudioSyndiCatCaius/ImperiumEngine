using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

// A singleton that handles a slice of game state. Activate creates it under the
// live game mode; Shutdown / Destroy tears it down and fires on_shutdown.
public abstract class C1_GameSystem : ImpComp
{
    // ################################################################################################################
    // STATIC
    // ################################################################################################################

    static C1_GameMode Mode_Get()
    {
        ImpGame game = ImpGame.current;
        if (game == null)
        {
            return null;
        }
        return game.game_mode;
    }

    public static C1_GameSystem GetActive(TClass<C1_GameSystem> system)
    {
        Type want = system.Get();
        if (want == null)
        {
            return null;
        }
        C1_GameMode mode = Mode_Get();
        if (mode == null || mode.active_states == null)
        {
            return null;
        }
        for (int i = 0; i < mode.active_states.Count; i++)
        {
            C1_GameSystem s = mode.active_states[i];
            if (s != null && s.GetType() == want)
            {
                return s;
            }
        }
        return null;
    }

    public static C1_GameSystem Activate(TClass<C1_GameSystem> system, Action on_shutdown = null)
    {
        Type t = system.Get();
        if (t == null || t.IsAbstract || !typeof(C1_GameSystem).IsAssignableFrom(t))
        {
            if (on_shutdown != null)
            {
                on_shutdown();
            }
            return null;
        }
        C1_GameSystem existing = GetActive(system);
        if (existing != null)
        {
            return existing;
        }
        C1_GameMode mode = Mode_Get();
        if (mode == null)
        {
            return null;
        }
        if (mode.active_states == null)
        {
            mode.active_states = new List<C1_GameSystem>();
        }

        C1_GameSystem sys = ImpComp.Create(t) as C1_GameSystem;
        if (sys == null)
        {
            return null;
        }
        if (IsBlocked(sys))
        {
            return null;
        }

        sys._on_shutdown = on_shutdown;
        // Register before shutting down blocked systems so a blocked system's
        // OnDestroy resume-pass sees this one and stays blocked.
        mode.active_states.Add(sys);
        ShutdownByTags(sys.blocked_systems);
        mode.Child_Add(sys);
        sys.OnBegin();
        return sys;
    }

    public static void SetListActive(List<TClass<C1_GameSystem>> systems, bool active)
    {
        if (systems == null)
        {
            return;
        }
        for (int i = 0; i < systems.Count; i++)
        {
            if (active)
            {
                Activate(systems[i]);
            }
            else
            {
                Shutdown(systems[i]);
            }
        }
    }

    public static bool Shutdown(TClass<C1_GameSystem> system)
    {
        C1_GameSystem sys = GetActive(system);
        if (sys == null)
        {
            return false;
        }
        sys.Destroy();
        return true;
    }

    public static bool IsActive(TClass<C1_GameSystem> system)
    {
        return GetActive(system) != null;
    }

    static bool IsBlocked(C1_GameSystem sys)
    {
        if (sys == null || sys.system_tags == null || sys.system_tags.IsEmpty)
        {
            return false;
        }
        C1_GameMode mode = Mode_Get();
        if (mode == null || mode.active_states == null)
        {
            return false;
        }
        for (int i = 0; i < mode.active_states.Count; i++)
        {
            C1_GameSystem s = mode.active_states[i];
            if (s == null || s == sys || s.blocked_systems == null)
            {
                continue;
            }
            if (s.blocked_systems.HasAny(sys.system_tags))
            {
                return true;
            }
        }
        return false;
    }

    static void ShutdownByTags(TTagSet tags)
    {
        if (tags == null || tags.IsEmpty)
        {
            return;
        }
        C1_GameMode mode = Mode_Get();
        if (mode == null || mode.active_states == null)
        {
            return;
        }
        List<C1_GameSystem> kill = new();
        for (int i = 0; i < mode.active_states.Count; i++)
        {
            C1_GameSystem s = mode.active_states[i];
            if (s == null || s.system_tags == null)
            {
                continue;
            }
            if (tags.HasAny(s.system_tags))
            {
                kill.Add(s);
            }
        }
        for (int i = 0; i < kill.Count; i++)
        {
            kill[i].Destroy();
        }
    }

    // ################################################################################################################
    // CLASS
    // ################################################################################################################

    [ImpVar] [Category("Tags")] public TTagSet system_tags = new();
    // Systems that will be shutdown and blocked from starting while this one is active
    [ImpVar] [Category("Tags")] public TTagSet blocked_systems = new();

    Action _on_shutdown;

    protected override void OnDestroy()
    {
        C1_GameMode mode = Mode_Get();
        if (mode != null && mode.active_states != null)
        {
            mode.active_states.Remove(this);
        }
        Action cb = _on_shutdown;
        _on_shutdown = null;
        if (cb != null)
        {
            cb();
        }
        if (mode != null && scene != null && scene.is_running)
        {
            mode.Persistent_TryActivate();
        }
        base.OnDestroy();
    }
}
