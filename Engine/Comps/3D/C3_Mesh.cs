using System.Numerics;
using Engine.Core;
using Engine.Structs;
using Engine.Assets;
using Engine.Globals;
using JoltPhysicsSharp;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;
using Mesh = R3D_cs.Mesh;


namespace Engine.Comps._3D;

[ImpClass(Common = true)][Title("Mesh Render")]
public class C3_Mesh : Imp3D
{
    [ImpVar] public A_Mesh mesh=A_Mesh.CUBE;
    [ImpVar] public List<A_Material> override_materials;

    public C3_Mesh()
    {
        physics_enabled = true;
        collision_preset = A_CollisionPreset.PRESET_MESH;
        render_config = A_RenderConfig.STATIC;
    }

    public override Shape? Phys_GetShape()
    {
        if (mesh == null) return base.Phys_GetShape();
        TBounds3 local = mesh.GetBounds();
        if (local.IsEmpty) return base.Phys_GetShape();
        Vector3 sc = global_transform.scale;
        Vector3 he = new(
            MathF.Abs(local.size.X * sc.X) * 0.5f,
            MathF.Abs(local.size.Y * sc.Y) * 0.5f,
            MathF.Abs(local.size.Z * sc.Z) * 0.5f);
        return ImpPhysics.MakeBox(he);
    }
    
    
    public override TBounds3 Bounds_Cache()
    {
        if (mesh == null) return new();
        return TBounds3.Offset(mesh.GetBounds(), global_transform, true);
    }

    public override void OnBegin()
    {
        base.OnBegin();
    }

    public override void OnDraw3D(double dt, EDrawFlags flags = 0)
    {
        base.OnDraw3D(dt, flags);
        if (mesh != null) mesh.Draw(global_transform, world_rotation, Drawing_ShadowsEnabled(), override_materials);
    }
    
}