using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Transit : Imp3D
{
    [ImpVar]  public TRef<ImpScene> linked_scene;
}