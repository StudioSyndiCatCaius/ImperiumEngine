using System.Numerics;
using ImperiumEngine.Assets.Materials;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Structs;
using R3D_cs;

namespace ImperiumEngine.Assets;

public class A_Mesh : ImpAsset
{
    // ################################################################################################################
    // Class
    // ################################################################################################################
    
    public Mesh mesh;
    public List<Mesh> meshes = new();
    public List<int> mesh_materials = new();
    [ImpVar] public List<A_Material> materials = new();

    public override void Source_OnReload(ImpFile file)
    {
        base.Source_OnReload(file);
        if (file.src_models.Count == 0)
        {
            return;
        }
        R3D_cs.Model mdl = file.src_models[0];
        int mesh_count = mdl.Meshes.Length;
        if (mesh_count <= 0)
        {
            return;
        }

        meshes.Clear();
        mesh_materials.Clear();
        for (int m = 0; m < mesh_count; m++)
        {
            meshes.Add(mdl.Meshes[m]);
        }

        int map_count = mdl.MeshMaterials.Length;
        for (int m = 0; m < mesh_count; m++)
        {
            int slot = m;
            if (m < map_count)
            {
                slot = mdl.MeshMaterials[m];
            }
            mesh_materials.Add(slot);
        }

        int i = source_index;
        if (i < 0 || i >= mesh_count)
        {
            i = 0;
        }
        mesh = meshes[i];

        if (materials == null)
        {
            materials = new List<A_Material>();
        }
        materials.Clear();
        int mat_count = mdl.Materials.Length;
        for (int m = 0; m < mat_count; m++)
        {
            Mat_Object existing = Material_Existing(file, m);
            if (existing != null)
            {
                materials.Add(existing);
                continue;
            }
            Mat_Object obj = new Mat_Object();
            obj.commons = TMaterialCommons.FromMaterial(mdl.Materials[m]);
            materials.Add(obj);
        }

        Mat_Object Material_Existing(ImpFile src, int index)
        {
            string from = src.filepath;
            try
            {
                from = Path.GetFullPath(from);
            }
            catch
            {
            }

            foreach (ImpAsset a in ImpAsset.Loaded_GetAll())
            {
                if (a is Mat_Object mat && Material_Matches(mat, from, index))
                {
                    return mat;
                }
            }

            string dir = Path.GetDirectoryName(src.filepath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return null;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.ImpMaterial");
            }
            catch
            {
                return null;
            }
            for (int f = 0; f < files.Length; f++)
            {
                if (ImpAsset.Load(files[f]) is Mat_Object mat && Material_Matches(mat, from, index))
                {
                    return mat;
                }
            }
            return null;
        }

        bool Material_Matches(Mat_Object mat, string source_path, int index)
        {
            if (mat.source_index != index)
            {
                return false;
            }
            if (mat.source_file == null || string.IsNullOrEmpty(mat.source_file.filepath))
            {
                return false;
            }
            string p = mat.source_file.filepath;
            try
            {
                p = Path.GetFullPath(p);
            }
            catch
            {
            }
            return string.Equals(p, source_path, StringComparison.OrdinalIgnoreCase);
        }
    }

    public int Submesh_Count()
    {
        if (meshes != null && meshes.Count > 0)
        {
            return meshes.Count;
        }
        if (mesh.VertexCount > 0)
        {
            return 1;
        }
        return 0;
    }

    public Mesh Submesh_Get(int index)
    {
        if (meshes != null && index >= 0 && index < meshes.Count)
        {
            return meshes[index];
        }
        return mesh;
    }

    public A_Material Material_Get(int submesh)
    {
        int slot = submesh;
        if (mesh_materials != null && submesh >= 0 && submesh < mesh_materials.Count)
        {
            slot = mesh_materials[submesh];
        }
        if (materials == null || slot < 0 || slot >= materials.Count)
        {
            return null;
        }
        return materials[slot];
    }

    public void Draw(Vector3 position, Quaternion rotation, Vector3 scale, List<A_Material> overrides, bool cast_shadows)
    {
        int count = Submesh_Count();
        for (int i = 0; i < count; i++)
        {
            Mesh gpu = Submesh_Get(i);
            if (gpu.VertexCount <= 0)
            {
                continue;
            }
            if (cast_shadows)
            {
                gpu.ShadowCastMode = ShadowCastMode.OnBackSide;
            }
            else
            {
                gpu.ShadowCastMode = ShadowCastMode.Disabled;
            }

            A_Material mat_asset = null;
            if (overrides != null && i < overrides.Count)
            {
                mat_asset = overrides[i];
            }
            if (mat_asset == null)
            {
                mat_asset = Material_Get(i);
            }

            R3D_cs.Material r3d;
            if (mat_asset != null)
            {
                r3d = mat_asset.Material_Get();
            }
            else
            {
                r3d = R3D.GetDefaultMaterial();
            }
            R3D.DrawMeshEx(gpu, r3d, position, rotation, scale);
        }
    }

    C2_Viewport3D _drop_view;
    C3_Mesh _drop_ghost;

    public override void SceneDrop_Enter(Imp2D view, ImpPlayer player)
    {
        SceneDrop_Exit(view, player);
        if (view is not C2_Viewport3D v3 || v3.view_scene == null)
        {
            return;
        }
        _drop_view = v3;
        _drop_ghost = new C3_Mesh { name = GetName(), mesh = this };
        v3.overlay = _drop_ghost;
        _drop_ghost.scene = v3.view_scene;
        SceneDrop_Update(view, 0, player);
    }

    public override void SceneDrop_Exit(Imp2D view, ImpPlayer player)
    {
        if (_drop_view != null && _drop_view.overlay == _drop_ghost)
        {
            _drop_view.overlay = null;
        }
        _drop_ghost?.Destroy();
        _drop_ghost = null;
        _drop_view = null;
    }

    public override void SceneDrop_Update(Imp2D view, float dt, ImpPlayer player)
    {
        if (_drop_ghost == null || _drop_view == null || player == null)
        {
            return;
        }
        if (_drop_view.Trace_World(player.cursor.position, out Vector3 pos, out _))
        {
            _drop_ghost.Position_Set(pos, true);
        }
    }

    public override ImpComp SceneDrop_DropOnComp(ImpComp comp, ImpPlayer player)
    {
        if (_drop_ghost == null)
        {
            return null;
        }
        ImpScene dest_scene = _drop_view?.view_scene ?? comp?.scene;
        ImpComp dest = dest_scene?.root;
        if (dest == null || dest == _drop_ghost || _drop_ghost.IsAncestorOf(dest))
        {
            SceneDrop_Exit(_drop_view, player);
            return null;
        }

        TTransform3 world = _drop_ghost.Transform_Get(true);
        if (_drop_view != null && _drop_view.overlay == _drop_ghost)
        {
            _drop_view.overlay = null;
        }

        dest.Child_Add(_drop_ghost);
        _drop_ghost.Transform_Set(world, true);
        ImpUndo.Comp_Moved(_drop_ghost, default, "Drop " + GetName());
        C3_Mesh spawned = _drop_ghost;
        _drop_ghost = null;
        _drop_view = null;
        return spawned;
    }
    
    // -------------------------------------------------
    // Bounds
    // -------------------------------------------------
    public TBounds3 Bounds_Get(TTransform3 transform)
    {
        int count = Submesh_Count();
        bool any = false;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < count; i++)
        {
            Mesh gpu = Submesh_Get(i);
            if (gpu.VertexCount <= 0)
            {
                continue;
            }
            Vector3 a_min = gpu.Aabb.Min;
            Vector3 a_max = gpu.Aabb.Max;
            min = Vector3.Min(min, a_min);
            max = Vector3.Max(max, a_max);
            any = true;
        }
        if (!any)
        {
            return TBounds3.ZERO;
        }
        Vector3 local_center = (min + max) * 0.5f;
        Vector3 local_size = max - min;
        Vector3 scl = new(
            MathF.Abs(transform.scale.X),
            MathF.Abs(transform.scale.Y),
            MathF.Abs(transform.scale.Z));
        Quaternion q = ImpMath.Euler_2_Quat(transform.rotation);
        return new TBounds3
        {
            center = transform.position + Vector3.Transform(local_center * transform.scale, q),
            size = local_size * scl,
            rotation = transform.rotation,
        };
    }

    // ################################################################################################################
    // Statics
    // ################################################################################################################
    
    static A_Mesh? _geo_cube;
    static A_Mesh? _geo_plane;
    static A_Mesh? _geo_sphere;

    public static A_Mesh GEO_CUBE => _geo_cube ??= new A_Mesh
    {
        mesh = R3D.GenMeshCube(1f, 1f, 1f),
        filepath = BuiltinPrefix + "A_Mesh.GEO_CUBE",
        materials = [ Mat_Surface.PROTO_TILE_WHITE]
    };
    public static A_Mesh GEO_PLANE => _geo_plane ??= new A_Mesh
    {
        mesh = R3D.GenMeshPlane(1f, 1f, 1, 1),
        filepath = BuiltinPrefix + "A_Mesh.GEO_PLANE",
        materials = [ Mat_Surface.PROTO_TILE_WHITE]
    };
    public static A_Mesh GEO_SPHERE => _geo_sphere ??= new A_Mesh
    {
        mesh = R3D.GenMeshSphere(0.5f, 32, 48),
        filepath = BuiltinPrefix + "A_Mesh.GEO_SPHERE",
        materials = [ Mat_Surface.PROTO_TILE_WHITE]
    };
    
    public static A_Mesh SK_MANNEQUIN=Import<A_Mesh>("{engine}/Meshes/Character/Mannequin/sk_c_mannequin.glb");
    
    public static A_Mesh UTIL_CAMERA=Import<A_Mesh>("{engine}/Meshes/Util/sm_edtior_util_camera.glb");
}