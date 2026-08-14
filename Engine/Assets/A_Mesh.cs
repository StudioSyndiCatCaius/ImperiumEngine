using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Structs;
using R3D_cs;

namespace ImperiumEngine.Assets;

public class A_Mesh : ImpAsset
{
    public Mesh mesh;

    static A_Mesh? _geo_cube;
    static A_Mesh? _geo_plane;

    public static A_Mesh GEO_CUBE => _geo_cube ??= new A_Mesh
    {
        mesh = R3D.GenMeshCube(1f, 1f, 1f),
        filepath = BuiltinPrefix + "A_Mesh.GEO_CUBE",
    };
    public static A_Mesh GEO_PLANE => _geo_plane ??= new A_Mesh
    {
        mesh = R3D.GenMeshPlane(1f, 1f, 1, 1),
        filepath = BuiltinPrefix + "A_Mesh.GEO_PLANE",
    };

    C2_SceneView _drop_view;
    C3_Mesh _drop_ghost;

    public override void SceneDrop_Enter(C2_SceneView view, ImpPlayer player)
    {
        SceneDrop_Exit(view, player);
        if (view?.scene == null) return;
        _drop_view = view;
        _drop_ghost = new C3_Mesh { name = GetName(), mesh = this };
        view.drop_preview = _drop_ghost;
        _drop_ghost.scene = view.scene;
        SceneDrop_Update(view, 0, player);
    }

    public override void SceneDrop_Exit(C2_SceneView view, ImpPlayer player)
    {
        if (view != null && view.drop_preview == _drop_ghost) view.drop_preview = null;
        _drop_ghost?.Destroy();
        _drop_ghost = null;
        _drop_view = null;
    }

    public override void SceneDrop_Update(C2_SceneView view, float dt, ImpPlayer player)
    {
        if (_drop_ghost == null || view == null) return;
        if (view.Drop_World3(player, out Vector3 pos, out _))
            _drop_ghost.Position_Set(pos, true);
    }

    public override void SceneDrop_DropOnComp(ImpComp comp, ImpPlayer player)
    {
        if (_drop_ghost == null) return;
        ImpComp dest = comp;
        ImpScene dest_scene = _drop_view?.scene ?? dest?.scene;
        if (dest == null || dest == _drop_ghost || _drop_ghost.IsAncestorOf(dest))
            dest = dest_scene?.root;
        if (dest == null)
        {
            SceneDrop_Exit(_drop_view, player);
            return;
        }

        TTransform3 world = _drop_ghost.Transform_Get(true);
        if (_drop_view != null && _drop_view.drop_preview == _drop_ghost)
            _drop_view.drop_preview = null;

        dest.Child_Add(_drop_ghost);
        _drop_ghost.Transform_Set(world, true);
        ImpUndo.Comp_Moved(_drop_ghost, default, "Drop " + GetName());
        _drop_view?.gizmo_data?.Selection_Set(new[] { (ImpComp)_drop_ghost });
        _drop_ghost = null;
        _drop_view = null;
    }
}