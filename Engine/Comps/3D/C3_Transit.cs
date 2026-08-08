using ImperiumEngine.Main;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._3D;

public class C3_Transit : ImpComp3D
{
    [ImpVar][Export] public TRef<ImpScene> linked_scene;
}