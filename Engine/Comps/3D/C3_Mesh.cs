using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using R3D_cs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Common = true)]
public class C3_Mesh : Imp3D
{
    [ImpVar] public A_Mesh mesh=A_Mesh.GEO_CUBE;
    [ImpVar] public List<A_Material> materials;
    [ImpVar] public bool cast_shadows=true;

    public C3_Mesh()
    {
        physics_enabled=true;
        collision_preset=A_CollisionPreset.PRESET_MESH;
    }
    
    public override void OnDraw3D(double dt, WDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (mesh == null || mesh.mesh.VertexCount <= 0) return;

        TTransform3 t = Transform_Get(true);
        float d = MathF.PI / 180f;
        Quaternion rot = Quaternion.CreateFromYawPitchRoll(t.rotation.Y * d, t.rotation.X * d, t.rotation.Z * d);
        R3D.DrawMeshEx(mesh.mesh, R3D.GetDefaultMaterial(), t.position, rot, t.scale);
    }
}
