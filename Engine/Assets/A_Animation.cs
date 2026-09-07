using Engine.Comps._1D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Assets;

public struct TAnimationKey
{
    [ImpVar] public float time;
    [ImpVar] public object value;
}

/*
 *  a fully custom animated sequence like in UE. VERY similar to how Sequences in Unreal Engine work
 */
public class A_Animation : ImpAsset
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar] public float duration=5.0f;
    [ImpVar] public List<A_Animation_Track> tracks;
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    [Builtin] public static A_Animation A2D_FADE_OPACITY = new() //fades the root (Imp2D) opacity from 1 to 0 over 0.5 seconds
    {
        tracks = new()
        {
            new AnimTrack_Var()
            {
                type = typeof(Imp2D),
                subtracks = new List<A_Animation_Track>()
                {
                    new AnimTrack_Var()
                    {
                        var_name = "opacity",
                        keys = new() { new() { value = 1.0f, time = 0.0f }, new() { value = 0.0f, time = 0.5f }, }
                    }
                }
            }
        }
    }; 

}

public abstract class A_Animation_Track  
{
    public bool singleton=false; // only one instance of this track is allowed per A_Animation
    
    [ImpVar] public string name;
    [ImpVar] public List<TAnimationKey> keys;
    [ImpVar] public List<A_Animation_Track> subtracks;
}

// ###################################################################################################################
// TRACKS
// ###################################################################################################################

// place references to Imp3D objects here, cutting between their Camera3D
public class AnimTrack_CameraCut : A_Animation_Track
{
    [ImpVar] public Dictionary<TGuid32,TRef<Imp3D>> cameras; // a key value is the guid int, that links to the Imp3D object. 
    public AnimTrack_CameraCut()
    {
        singleton=true;
    }
}


// edit a var belonging to an object
public class AnimTrack_Var : A_Animation_Track
{
    public string binding;
    public string var_name;
    public Type type;
}


public class AnimTrack_Sound : A_Animation_Track // plays a sound
{
    [ImpVar] public A_Sound sound;
    [ImpVar] public float volume=1.0f;
    [ImpVar] public float pitch=1.0f;
    [ImpVar] public float fade_in=0.0f;
    [ImpVar] public float fade_out = 0.0f;
}
