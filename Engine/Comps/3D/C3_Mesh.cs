using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Structs;
using JoltPhysicsSharp;
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
    
    public override void OnDraw3D(double dt, EDrawFlags flags)
    {
        base.OnDraw3D(dt, flags);
        if (mesh == null || mesh.mesh.VertexCount <= 0) return;

        TTransform3 t = Transform_Get(true);
        float d = MathF.PI / 180f;
        Quaternion rot = Quaternion.CreateFromYawPitchRoll(t.rotation.Y * d, t.rotation.X * d, t.rotation.Z * d);
        R3D.DrawMeshEx(mesh.mesh, R3D.GetDefaultMaterial(), t.position, rot, t.scale);
    }

    public override Shape Phys_MakeShape(Vector3 world_scale)
    {
        Vector3 s = new(
            MathF.Abs(world_scale.X),
            MathF.Abs(world_scale.Y),
            MathF.Abs(world_scale.Z));
        if (s.X < 0.01f)
        {
            s.X = 0.01f;
        }
        if (s.Y < 0.01f)
        {
            s.Y = 0.01f;
        }
        if (s.Z < 0.01f)
        {
            s.Z = 0.01f;
        }

        if (mesh != null && ReferenceEquals(mesh, A_Mesh.GEO_PLANE))
        {
            return new BoxShape(new Vector3(s.X * 0.5f, MathF.Max(0.02f, s.Y * 0.02f), s.Z * 0.5f));
        }
        return new BoxShape(s * 0.5f);
    }
}
