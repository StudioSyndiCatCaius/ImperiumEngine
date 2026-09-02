using System.Numerics;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;
using R3D_cs;

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
        return Source_Get().get_Model(model_index);
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

    public void Draw(TTransform3 transform, bool shadows = true)
    {
        ImpFile src = Source_Get();
        if (src == null || model_index < 0 || model_index >= src.src_models.Count) return;
        Model m = src.src_models[model_index];
        ShadowCastMode mode = shadows ? Imp3D.shadow_cast_mode : ShadowCastMode.Disabled;
        Span<Mesh> meshes = m.Meshes;
        for (int i = 0; i < meshes.Length; i++)
        {
            Mesh mesh = meshes[i];
            if (mesh.ShadowCastMode == mode) continue;
            mesh.ShadowCastMode = mode;
            meshes[i] = mesh;
        }
        R3D.DrawModelEx(m, transform.position, GMath.Euler_2_Quat(transform.rotation), transform.scale);
    }
    
    // ================================================================================================================
    // STATIC
    // ================================================================================================================
    
    public static string PATH_SHAPE_CUBE = "{engine}/3D/Shapes/sm_ED_shape_cube.ImpAsset";
    public static string PATH_SHAPE_PLANE = "{engine}/3D/Shapes/sm_ED_shape_plane.ImpAsset";
    public static string PATH_SHAPE_SPHERE = "{engine}/3D/Shapes/sm_ED_shape_sphere.ImpAsset";
}