using ImperiumEngine.Assets;
using ImperiumEngine;

namespace ImperiumEngine.Comps._3D;

public class C3_Audio : ImpComp3D
{
    [ImpVar] public A_Sound sound;
    [ImpVar] public float volume=1.0f;
    [ImpVar] public bool loop=false;
}