using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

// A singleton that handles a slice of game state. Activate creates it under the
// live game mode; Shutdown / Kill tears it down and fires on_shutdown. Systems are singletons. Only one per type allowed.
public abstract class C1_GameSystem : Imp1D
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATICS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    private static List<C1_GameSystem> systems_active=new ();
    
    //all systems attached to persistent root.
    public static ImpComp GetPersistentSceneRoot()
    {
        return App.scene_persistent.root;
    }

    public static void SetListActive(List<TClass<C1_GameSystem>> Systems, object context=null, bool active = true)
    {
        foreach (var s in Systems)
        {
            if(active) { Activate(s, context); }
            else { Shutdown(s, context); }
        }
    }
    
    public static C1_GameSystem Activate(TClass<C1_GameSystem> System,object context=null,Action on_shutdown=null)
    {
        if (CanSystemActivate(System))
        {
            C1_GameSystem sys = System.Spawn(GetPersistentSceneRoot(), (c) =>
            {
                C1_GameSystem s = c as C1_GameSystem;
                s.system_context = context;
            }) as C1_GameSystem;
            if (sys == null) return null;
            sys.on_end += (c) =>
            {
                on_shutdown?.Invoke();
            };
            systems_active.Add(sys);
            return sys;
        }
        return null;
    }
    public static bool Shutdown(TClass<C1_GameSystem> System,object context=null)
    {
        foreach (var s in systems_active)
        {
            if (s.GetType() == System.Get())
            {
                s.Kill();
                return true;
            }
        }
        return false;
    }

    public static bool IsTagActive(TTag tag)
    {
        foreach (var s in systems_active)
        {
            if (s.system_tags != null && s.system_tags.Has(tag))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsTagBlocked(TTag tag)
    {
        foreach (var s in systems_active)
        {
            if (s.blocked_system_tags != null && s.blocked_system_tags.Has(tag))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsSystemActive(TClass<C1_GameSystem> System)
    {
        Type type = System.Get();
        if (type == null) return false;
        foreach (var s in systems_active)
        {
            if (s.GetType() == type)
            {
                return true;
            }
        }
        return false;
    }

    public static bool CanSystemActivate(TClass<C1_GameSystem> System)
    {
        if (IsSystemActive(System)) return false;

        Type type = System.Get();
        if (type == null || type.IsAbstract) return false;

        C1_GameSystem probe = Activator.CreateInstance(type) as C1_GameSystem;
        if (probe?.system_tags == null) return true;

        foreach (TTag tag in probe.system_tags)
        {
            if (IsTagBlocked(tag)) return false;
        }
        return true;
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    [ImpVar] public TTagSet system_tags;
    [ImpVar] public TTagSet blocked_system_tags;
    
    public object system_context;
    
    public override void OnBegin()
    {
        base.OnBegin();
        ImpPlayer.Get().input_targets.Add(this);
    }

    public override void OnEnd()
    {
        base.OnEnd();
        ImpPlayer.Get().input_targets.Remove(this);
        if (systems_active.Contains(this))
        {
            systems_active.Remove(this);
        }
    }
}
