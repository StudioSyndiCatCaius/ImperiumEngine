using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;

namespace ImperiumEngine.Objects._3D;

public class C3_Audio : ImpComponent3D
{
    [ImpVar] public A_Sound sound;
    [ImpVar] public bool is_looping;
    [ImpVar] public float volume;
}