using ImperiumEngine;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Assets;

public struct TAnimationTrack
{
    public TLabel var_name;
    public List<TAnimationKey> keys;
}

public struct TAnimationKey
{
    public float time;
    public object value;
}

[AssetColor(230, 120, 170)]
public class A_Animation : ImpAsset
{
    [ImpVar] public int fps=60;
    [ImpVar] public List<TAnimationTrack> tracks;
    
    // ---------------------------------------------------------------------------------
    // Static
    // ---------------------------------------------------------------------------------

    //creates a new animation of a simple fade effect.
    public A_Animation New_Fade(Imp2D comp, float duration = 0.5f)
    {
        A_Animation anim = new();
        return anim;
    }
}