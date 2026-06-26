using ImperiumCore.Assets;
using ImperiumCore.Classes;

namespace ImperiumCore.Structs;

public struct TModel_Mesh
{
    //im still learning 3D. Mesh matrix/vertex data goes in a param here
    // public TMatrix4x4 Matrix;
    public List<A_Material> Materials;
}

public class TMatrix4x4
{
    public float[] data;
    public int size;
}