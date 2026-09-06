using Engine.Assets;
using Engine.Core;

namespace Engine.Comps._3D;

public class C3_Audio : Imp3D
{
    [ImpVar] public A_Sound sound;
    [ImpVar] public A_SoundAttenuation override_attenuation;


    public override void OnDrawDebug(double dt, bool drawing_3d)
    {
        base.OnDrawDebug(dt, drawing_3d);
        if (drawing_3d)
        {
            //draw billboard audio icon
            //draw attenuation radius
        }
    }
}