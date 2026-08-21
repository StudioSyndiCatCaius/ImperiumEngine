using ImperiumEngine.Assets;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public enum EAnimatorState
{
    Stopped,
    Playing,
    Paused,
}

// can play an A_Animation
public class C1_Animator : ImpComp
{
    [ImpVar] public A_Animation animation;
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