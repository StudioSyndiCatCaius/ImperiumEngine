using ImperiumCore.Assets;
using ImperiumCore.Classes;
using ImperiumCore.Structs;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects._1D;

namespace ImperiumEngine.Objects._3D.Prim;

public class O3D_Mesh : O3D_Primitive
{
    public A_Mesh? mesh;

    protected internal override void OnDraw3D(ImpRender rhi, O1D_Viewport viewport, ImpCamera camera, double dt)
    {
        if (mesh == null || mesh.modelMesh.vertices == null) return;
        rhi.Draw3D_MeshFast(transform, mesh.modelMesh);
    }

    // ===== factories =====

    public static TModel_Mesh Mesh_Cube(float size = 1f)
    {
        float s = size * 0.5f;

        // interleaved: pos.xyz + normal.xyz (6 floats per vertex, 4 verts per face, 6 faces)
        float[] verts =
        [
            // +X face
            s, s, s, 1,0,0,    s,-s, s, 1,0,0,    s,-s,-s, 1,0,0,    s, s,-s, 1,0,0,
            // -X face
           -s, s,-s,-1,0,0,   -s,-s,-s,-1,0,0,   -s,-s, s,-1,0,0,   -s, s, s,-1,0,0,
            // +Y face
           -s, s,-s, 0,1,0,    s, s,-s, 0,1,0,    s, s, s, 0,1,0,   -s, s, s, 0,1,0,
            // -Y face
           -s,-s, s, 0,-1,0,   s,-s, s, 0,-1,0,   s,-s,-s, 0,-1,0,  -s,-s,-s, 0,-1,0,
            // +Z face
           -s, s, s, 0,0,1,   -s,-s, s, 0,0,1,    s,-s, s, 0,0,1,    s, s, s, 0,0,1,
            // -Z face
            s, s,-s, 0,0,-1,   s,-s,-s, 0,0,-1,  -s,-s,-s, 0,0,-1,  -s, s,-s, 0,0,-1,
        ];

        uint[] indices = new uint[36];
        for (uint f = 0; f < 6; f++)
        {
            uint b = f * 4;
            uint i = f * 6;
            indices[i+0] = b+0; indices[i+1] = b+1; indices[i+2] = b+2;
            indices[i+3] = b+0; indices[i+4] = b+2; indices[i+5] = b+3;
        }

        return new TModel_Mesh { vertices = verts, indices = indices };
    }
}
