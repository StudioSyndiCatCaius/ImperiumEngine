using System.Numerics;
using Engine.Assets;
using Engine.Core;
using Engine.Structs;

namespace Engine.Comps._1D;

public enum EAnimatorState
{
    Stopped,
    Playing,
    Paused,
}

public struct TAnimationPlayConfig
{
    [ImpVar] public float speed=1.0f;
    [ImpVar] public bool loop=false;
    [ImpVar] public Dictionary<TLabel, TRef<ImpComp>> bindings;

    public TAnimationPlayConfig()
    {
        
    }
}


// can play an A_SkeletonAnim
public class C1_Animator : Imp1D
{
    [ScriptCall]
    public static C1_Animator Play(A_Animation anim, TAnimationPlayConfig config=default,Action<bool> on_paused=null,Action on_finish=null)
    {
        // spawns a `C1_Animator` attached to the current scene root
        return null;
    }
    
    [ImpVar] public A_SkeletonAnim SkeletonAnim;
    [ImpVar] public Dictionary<TLabel, ImpComp> comp_bindings;

    public EAnimatorState state = EAnimatorState.Stopped;

    public void Play()
    {
        state = EAnimatorState.Playing;
    }
    public void Pause()
    {
        state = EAnimatorState.Paused;
    }
    public void Stop()
    {
        state = EAnimatorState.Stopped;
    }
    
    public Action<C1_Animator> on_animation_begin;
    public Action<C1_Animator,bool> on_animation_pause;
    public Action<C1_Animator> on_animation_end;
}