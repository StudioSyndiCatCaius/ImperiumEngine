using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._3D;

//Baseclass for all 3D Primitives, that can be rendered with materials
public class O3D_Primitive : ImpComponent3D
{
    public TMaterialConfig Materials;
}