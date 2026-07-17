using ImperiumCore.Classes;
using ImperiumCore.Structs;

namespace ImperiumCore.Assets;

public class A_Mesh : ModelRef
{
    public TModel_Mesh modelMesh;

    public A_Mesh()
    {
        ModelType = EModelRefType.Mesh;
    }
}
