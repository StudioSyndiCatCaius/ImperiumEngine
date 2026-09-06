using System.Numerics;
using Engine.Core;

namespace Engine.Assets;

public class A_Sound : ImpAsset
{
    [ImpVar] public A_SoundAttenuation default_attenuation;
}

public enum ESoundAttenuationVolume
{
    Sphere, 
    Box
}

public class A_SoundAttenuation : ImpAsset
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar] public ESoundAttenuationVolume volume_type;
    
    [ImpVar] public float distance_min; //within this distance, the sound will be heard at full volume 
    [ImpVar] public float distance_max; //outside this distance, the sound will be heard at 0% volume
    
    //for box
    [ImpVar] public Vector3 distance_min_v; //within this distance, the sound will be heard at full volume 
    [ImpVar] public Vector3 distance_max_v; //outside this distance, the sound will be heard at 0% volume

    public float GetVolume(Vector3 position)
    {
        return 1.0f;
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATICS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [Builtin] public static A_SoundAttenuation DEFAULT=new ()
    {
        
    };
    [Builtin] public static A_SoundAttenuation FOOTSTEP=new ()
    {
        
    };
    [Builtin] public static A_SoundAttenuation VOICE=new ()
    {
        
    };
    [Builtin] public static A_SoundAttenuation EXPLOSION=new ()
    {
        
    };
}