using ImperiumEngine.Assets;
using ImperiumEngine.Main;
using R3D_cs;
using Mesh = R3D_cs.Mesh;

namespace ImperiumEngine.Comps._3D;

public class C3_Mesh : ImpComp3D
{
    [ImpVar][Export] public A_Mesh? mesh;
    [ImpVar][Export] public A_Material[] materials = [];

    // Submits the mesh to whichever R3D session is open around this draw. Nothing is
    // rasterised here: R3D collects draw calls and renders them all in R3D.End().
    public override void OnDraw(double dt, EDrawFlags flags)
    {
        base.OnDraw(dt, flags);
        if (!is_visible) return;

        if (mesh?.get_Mesh() is not Mesh r3d_mesh) return;

        // R3D draws one material per mesh; the array is there for multi-part models to
        // grow into, so the first slot is the one that counts.
        var material = materials.Length > 0 && materials[0] != null
            ? materials[0].get_Material()
            : R3D.GetDefaultMaterial();

        R3D.DrawMeshPro(r3d_mesh, material, Matrix_GetWorld());
    }
}
