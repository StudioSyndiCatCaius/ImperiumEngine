using ImperiumCore.Assets;

namespace ImperiumCore.Structs;

public struct TModel_Mesh
{
    // interleaved: pos.xyz + normal.xyz = 6 floats per vertex
    public float[]? vertices;
    public uint[]?  indices;
    public List<A_Material>? Materials;
}

public class TMatrix4x4
{
    public float[] data;
    public int size;
}
