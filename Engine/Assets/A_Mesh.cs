using System.Numerics;
using Engine.Assets.Materials;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;
using R3D_cs;
using Material = R3D_cs.Material;

namespace Engine.Assets;

public class A_Mesh : ImpAsset
{
    // ================================================================================================================
    // CLASS
    // ================================================================================================================
    [ImpVar] public int model_index=0;
    [ImpVar] public List<A_Material> materials;

    public Model getModel()
    {
        ImpFile src = Source_Get();
        if (src == null) return default;
        return src.get_Model(model_index);
    }

    public TBounds3 GetBounds()
    {
        ImpFile src = Source_Get();
        if (src == null || model_index < 0 || model_index >= src.src_models.Count) return default;
        Model m = src.src_models[model_index];
        Vector3 min = m.Aabb.Min;
        Vector3 max = m.Aabb.Max;
        if ((max - min).LengthSquared() < 1e-16f)
        {
            Span<Mesh> meshes = m.Meshes;
            if (meshes.Length > 0)
            {
                min = new Vector3(float.MaxValue);
                max = new Vector3(float.MinValue);
                for (int i = 0; i < meshes.Length; i++)
                {
                    min = Vector3.Min(min, meshes[i].Aabb.Min);
                    max = Vector3.Max(max, meshes[i].Aabb.Max);
                }
            }
        }
        if ((max - min).LengthSquared() < 1e-16f) return default;
        return new TBounds3
        {
            center = (min + max) * 0.5f,
            size = max - min,
            rotation = Vector3.Zero,
        };
    }

    ShadowCastMode _shadow_applied = (ShadowCastMode)int.MaxValue;

    public void Draw(TTransform3 transform, Quaternion rotation, bool shadows = true, List<A_Material>? overrides = null)
    {
        ImpFile src = Source_Get();
        if (src == null || model_index < 0 || model_index >= src.src_models.Count) return;
        Model m = src.src_models[model_index];
        ShadowCastMode mode = shadows ? Imp3D.shadow_cast_mode : ShadowCastMode.Disabled;
        Span<Mesh> meshes = m.Meshes;
        if (_shadow_applied != mode)
        {
            _shadow_applied = mode;
            for (int i = 0; i < meshes.Length; i++)
            {
                Mesh mesh = meshes[i];
                if (mesh.ShadowCastMode == mode) continue;
                mesh.ShadowCastMode = mode;
                meshes[i] = mesh;
            }
        }
        List<A_Material> mats = overrides != null && overrides.Count > 0 ? overrides : materials;
        if (mats == null || mats.Count == 0)
        {
            R3D.DrawModelEx(m, transform.position, rotation, transform.scale);
            return;
        }
        Span<int> mesh_mats = m.MeshMaterials;
        for (int i = 0; i < meshes.Length; i++)
        {
            int mi = i < mesh_mats.Length ? mesh_mats[i] : i;
            if (mi < 0) mi = 0;
            if (mi >= mats.Count) mi = mats.Count - 1;
            A_Material src_mat = mats[mi];
            Material gpu = src_mat != null ? src_mat.Material_Get() : R3D.GetDefaultMaterial();
            R3D.DrawMeshEx(meshes[i], gpu, transform.position, rotation, transform.scale);
        }
    }

    public void Draw(TTransform3 transform, bool shadows = true)
    {
        Draw(transform, GMath.Euler_2_Quat(transform.rotation), shadows);
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    [Builtin] public static A_Mesh CUBE = new()
    {
        sourcefile = "{engine}/3D/Shapes/sm_ED_shape_cube.glb" ,
        materials = new(){ A_M3_Surface.PROTO_WHITE}
    };
    [Builtin] public static A_Mesh SPHERE = new()
    {
        sourcefile = "{engine}/3D/Shapes/sm_ED_shape_sphere.glb",
        materials = new(){ A_M3_Surface.PROTO_WHITE}
    };
    [Builtin] public static A_Mesh PLANE = new()
    {
        sourcefile = "{engine}/3D/Shapes/sm_ED_shape_plane.glb",
        materials = new(){ A_M3_Surface.PROTO_WHITE}
    };
    [Builtin] public static A_Mesh CYLINDER = new()
    {
        sourcefile = "{engine}/3D/Shapes/sm_ED_shape_cylinder.glb",
        materials = new(){ A_M3_Surface.PROTO_WHITE}
    };
    [Builtin] public static A_Mesh CONE = new()
    {
        sourcefile = "{engine}/3D/Shapes/sm_ED_shape_cone.glb",
        materials = new(){ A_M3_Surface.PROTO_WHITE}
    };
    [Builtin] public static A_Mesh RAMP = new()
    {
        sourcefile = "{engine}/3D/Shapes/sm_ED_shape_ramp.glb",
        materials = new(){ A_M3_Surface.PROTO_WHITE}
    };
    
    [Builtin] public static A_Mesh MANNEQUIN=new() { sourcefile = "{engine}/3D/Character/QuatManeq/sk_QuaManneq_anims1_IP.glb" };
    
}