using System.Numerics;
using Editor.Panel;
using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.EditMode;

public class EditMode_Comp : EdEditMode
{
    public TGizmoData gizmo_data = new() { space = EGizmoSpace.Local };

    C3_Gizmo gizmo3d = new();
    C2_Gizmo gizmo2d = new();

    bool marquee;
    bool marquee_add;
    Vector2 marquee_a;
    Vector2 marquee_b;
    const float marquee_min = 4f;

    ImpAsset _drop_asset;
    ImpComp _drop_comp;
    Type _drop_type;
    ImpComp _drop_type_ghost;

    public List<ImpComp> selected_comps => gizmo_data.selected_comps;

    public override bool IsBusy => marquee || gizmo3d.is_dragging || gizmo2d.is_dragging;
    public override bool BlocksCamera => marquee || gizmo3d.IsBusy || gizmo2d.IsBusy;
    public override bool BlocksWheel => marquee;

    public EditMode_Comp()
    {
        gizmo3d.gizmo_data = gizmo_data;
        gizmo2d.gizmo_data = gizmo_data;
    }

    public override void OnBegin()
    {
        gizmo3d.gizmo_data = gizmo_data;
        gizmo2d.gizmo_data = gizmo_data;
    }

    public override void OnEnd()
    {
        CancelInteraction();
    }

    public override void OnHidden()
    {
        CancelInteraction();
    }

    public override void OnUpdate(double dt)
    {
        if (view == null || ImpPlayer.players.Count == 0)
        {
            return;
        }
        ImpPlayer player = ImpPlayer.players[0];
        TDimensions2 vp = view.ActiveViewDim();
        bool over = player.Cursor_IsInDimensions(vp);
        bool ours = player.target_focus == view || view.IsChildFocus(player.target_focus);
        bool alt = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftAlt) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightAlt);
        bool shift = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftShift) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightShift);
        bool lmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left);
        bool rmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Right);
        bool ctrl = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl)
            || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);
        bool lmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left);
        bool ui_block = player.target_cursor is C2_Seperator
            || player.input_hog is C2_Seperator
            || player.grab_is_active;

        gizmo_data.snap_active = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl)
            || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);

        // --------- GIZMO MODE ---------
        Dictionary<EInputKey, EGizmoMode> gizmo_mode_keys = new()
        {
            { EInputKey.Key_W, EGizmoMode.Translate },
            { EInputKey.Key_E, EGizmoMode.Rotate },
            { EInputKey.Key_R, EGizmoMode.Scale },
        };
        if (!ctrl)
        {
            foreach (KeyValuePair<EInputKey, EGizmoMode> kvp in gizmo_mode_keys)
            {
                if (ImpPlayer.Key_IsHeld(kvp.Key))
                {
                    gizmo_data.mode = kvp.Value;
                }
            }
        }
        
        // --------- FOCUS
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_F))
        {
            Focus_Selection();
        }
        
        // --------- GIZMO SPACE
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_T))
        {
            if (gizmo_data.space == EGizmoSpace.Local)
            {
                gizmo_data.space = EGizmoSpace.World;
            }
            else
            {
                gizmo_data.space = EGizmoSpace.Local;
            }
        }
        // --------- Clear selection
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Escape))
        {
            if (ImpPlayer.TargetGame_IsHost())
            {
                gizmo_data.Selection_Clear();
            }
        }
        
        // --------- CTRL Handle
        if (ctrl)
        {
            // --------- Edit
            if (ImpPlayer.Key_IsPressed(EInputKey.Key_E))
            {
                ImpComp top = gizmo_data.FirstSelected();
                if (top != null && top.IsPackedForeign)
                {
                    top = top.packed_from;
                }
                if (top != null && top.IsInstanceRoot)
                {
                    ImpScene packed = top.packed.Get();
                    if (packed != null)
                    {
                        ImpAsset.Editor_OnOpenAsset?.Invoke(packed);
                    }
                }
            }
        }

        // ----------------------------------------------------
        // Gizmo handle
        // ----------------------------------------------------
        bool allow_gizmo = over && ours && !alt && !ui_block;
        bool giz_busy = false;
        if (view.view_mode == ESceneEditView.Mode_3D)
        {
            giz_busy = gizmo3d.Interact(view.viewport3D.camera, vp, player, view, allow_gizmo);
        }
        else
        {
            giz_busy = gizmo2d.Interact(view.viewport2D.camera, vp, player, view, allow_gizmo);
        }

        if (marquee)
        {
            marquee_b = player.cursor.position;
            if (view.view_mode == ESceneEditView.Mode_3D && rmb)
            {
                marquee = false;
                if (player.input_hog == view)
                {
                    player.input_hog = null;
                }
                view.Camera_BeginWalk(player);
                return;
            }
            if (!lmb)
            {
                marquee = false;
                if (player.input_hog == view)
                {
                    player.input_hog = null;
                }
                if (Vector2.Distance(marquee_a, marquee_b) < marquee_min)
                {
                    PickAt(marquee_b, marquee_add);
                }
                else
                {
                    PickRect(vp, marquee_a, marquee_b, marquee_add);
                }
            }
            return;
        }

        if (!giz_busy && over && ours && lmb_p && !alt && !ui_block)
        {
            marquee = true;
            marquee_add = shift;
            marquee_a = player.cursor.position;
            marquee_b = marquee_a;
            player.input_hog = view;
            return;
        }

        if (player.grab_is_active && over)
        {
            Drop_Tick(player, (float)dt);
            TypeDrop_Tick(player);
        }
    }

    public override void OnDraw2DForeground(double dt, EDrawFlags flags)
    {
        if (view == null)
        {
            return;
        }
        TDimensions2 vp = view.ActiveViewDim();
        if (vp.size.X <= 0 || vp.size.Y <= 0)
        {
            return;
        }

        double t_giz = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
        if (view.view_mode == ESceneEditView.Mode_3D)
        {
            gizmo3d.Draw(view.viewport3D.camera, vp, view.viewport3D.Root_Get());
        }
        else
        {
            gizmo2d.Draw(view.viewport2D.camera, vp);
        }
        if (ImpProfiler.enabled)
        {
            ImpProfiler.sv_gizmo += ImpProfiler.Now_Ms - t_giz;
        }

        if (marquee && Vector2.Distance(marquee_a, marquee_b) >= marquee_min)
        {
            Vector2 min = Vector2.Min(marquee_a, marquee_b);
            Vector2 size = Vector2.Max(marquee_a, marquee_b) - min;
            Raylib.DrawRectangleV(min, size, new Color(70, 160, 255, 40));
            Raylib.DrawRectangleLinesEx(new Rectangle(min.X, min.Y, size.X, size.Y), 1f, new Color(120, 200, 255, 220));
        }
    }

    public override void OnCursorUpdate(ImpPlayer player, double dt)
    {
        Drop_Tick(player, (float)dt);
        TypeDrop_Tick(player);
    }

    public override void OnGrabDrop(ImpPlayer player, ENotifyGrabTarget notify, ImpComp other, double dt)
    {
        if (notify == ENotifyGrabTarget.Hover_AsInstigator_Start)
        {
            Drop_Bind(Drop_AssetOf(other), player);
            TypeDrop_Bind(Drop_TypeOf(other), player);
        }
        else if (notify == ENotifyGrabTarget.Hover_AsInstigator_End)
        {
            Drop_Bind(null, player);
            TypeDrop_Bind(null, player);
        }
        else if (notify == ENotifyGrabTarget.Drop_AsInstigator)
        {
            if (_drop_asset == null)
            {
                _drop_asset = Drop_AssetOf(other);
            }
            if (_drop_asset != null)
            {
                ImpComp spawned = _drop_asset.SceneDrop_DropOnComp(view?.scene?.root, player);
                if (spawned != null)
                {
                    gizmo_data.Selection_Set(new[] { spawned });
                }
                Drop_Bind(null, player);
                TypeDrop_Bind(null, player);
                return;
            }
            if (_drop_type == null)
            {
                TypeDrop_Bind(Drop_TypeOf(other), player);
            }
            ImpComp made = TypeDrop_Commit(player);
            if (made != null)
            {
                gizmo_data.Selection_Set(new[] { made });
            }
            TypeDrop_Bind(null, player);
        }
    }

    public override void DuplicateSelected()
    {
        if (view?.scene?.root == null)
        {
            return;
        }
        List<ImpComp> src = SelectionRoots(gizmo_data.selected_comps);
        List<ImpComp> was_selected = new(gizmo_data.selected_comps);
        List<ImpComp> copies = new();
        ImpUndo.Group_Begin("Duplicate");
        for (int i = 0; i < src.Count; i++)
        {
            ImpComp s = src[i];
            if (s == null || s == view.scene.root || s.parent == null || s.IsPackedForeign || s.IsOwned)
            {
                continue;
            }
            ImpComp copy = s.Clone();
            if (copy == null)
            {
                continue;
            }
            copy.name = ImpComp.Name_Unique(s.parent, string.IsNullOrEmpty(s.name) ? copy.GetType().Name : s.name);
            int idx = s.parent.children.IndexOf(s);
            s.parent.Child_Insert(idx + 1, copy);
            if (copy is Imp3D c3)
            {
                c3.Position_Set(c3.Position_Get(false) + new Vector3(0.5f, 0f, 0f), false);
            }
            if (copy is Imp2D c2)
            {
                c2.Position_Set(c2.Position_Get(false) + new Vector2(16f, 16f), false);
            }
            copies.Add(copy);
            ImpUndo.Comp_Moved(copy, default, "Duplicate");
        }
        if (copies.Count > 0)
        {
            SelectionUndo(was_selected, copies);
            gizmo_data.Selection_Set(copies);
        }
        ImpUndo.Group_End();
    }

    public override void DeleteSelected()
    {
        if (view?.scene?.root == null)
        {
            return;
        }
        List<ImpComp> src = SelectionRoots(gizmo_data.selected_comps);
        List<ImpComp> was_selected = new(gizmo_data.selected_comps);
        ImpUndo.Group_Begin("Delete");
        for (int i = 0; i < src.Count; i++)
        {
            ImpComp s = src[i];
            if (s == null || s == view.scene.root || s.IsPackedForeign || s.IsOwned)
            {
                continue;
            }
            TCompPlace from = ImpUndo.Place_Get(s);
            s.Detach();
            ImpUndo.Comp_Moved(s, from, "Delete");
        }
        SelectionUndo(was_selected, new List<ImpComp>());
        gizmo_data.Selection_Clear();
        ImpUndo.Group_End();
    }

    void CancelInteraction()
    {
        marquee = false;
        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer player = ImpPlayer.players[0];
            if (view != null && player.input_hog == view)
            {
                player.input_hog = null;
            }
            Drop_Bind(null, player);
            TypeDrop_Bind(null, player);
        }
        else
        {
            TypeDrop_Clear();
        }
    }

    void PickAt(Vector2 screen, bool additive)
    {
        if (view?.scene?.root == null)
        {
            return;
        }
        ImpComp hit;
        if (view.view_mode == ESceneEditView.Mode_3D)
        {
            hit = Imp3D.Select(view.viewport3D.Root_Get(), view.viewport3D.Trace_Ray(screen), out _);
        }
        else
        {
            hit = view.viewport2D.Trace_Pick(screen);
        }

        hit = ImpComp.OutlinerHost(hit);
        if (hit == null)
        {
            if (!additive)
            {
                gizmo_data.Selection_Clear();
            }
            return;
        }
        if (additive)
        {
            gizmo_data.Selection_Toggle(hit);
        }
        else
        {
            gizmo_data.Selection_Set(new[] { hit });
        }
    }

    void PickRect(TDimensions2 vp, Vector2 a, Vector2 b, bool additive)
    {
        if (view?.scene?.root == null)
        {
            return;
        }
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        List<ImpComp> hits = new();
        if (view.view_mode == ESceneEditView.Mode_3D)
        {
            C3_Gizmo.PickRect(view.scene.root, view.viewport3D.camera, vp, min, max, hits);
        }
        else
        {
            C2_Gizmo.PickRect(view.scene.root, view.viewport2D.camera, vp, min, max, hits);
        }

        List<ImpComp> hosts = new();
        for (int i = 0; i < hits.Count; i++)
        {
            ImpComp h = ImpComp.OutlinerHost(hits[i]);
            if (h == null)
            {
                continue;
            }
            bool already = false;
            for (int j = 0; j < hosts.Count; j++)
            {
                if (hosts[j] == h)
                {
                    already = true;
                    break;
                }
            }
            if (!already)
            {
                hosts.Add(h);
            }
        }

        if (additive)
        {
            gizmo_data.Selection_Add(hosts);
        }
        else
        {
            gizmo_data.Selection_Set(hosts);
        }
    }

    void Focus_Selection()
    {
        if (view == null)
        {
            return;
        }
        List<ImpComp> sel = gizmo_data.selected_comps;

        if (view.view_mode == ESceneEditView.Mode_3D)
        {
            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);
            Vector3[] corners = new Vector3[8];
            int count = 0;
            for (int i = 0; i < sel.Count; i++)
            {
                if (sel[i] is not Imp3D c3)
                {
                    continue;
                }
                TBounds3 b = c3.Bounds_Get();
                if (b.IsEmpty)
                {
                    continue;
                }
                b.Corners(corners);
                for (int k = 0; k < 8; k++)
                {
                    min = Vector3.Min(min, corners[k]);
                    max = Vector3.Max(max, corners[k]);
                }
                count++;
            }
            if (count == 0)
            {
                return;
            }
            Vector3 center = (min + max) * 0.5f;
            float radius = MathF.Max(0.25f, Vector3.Distance(max, min) * 0.5f);
            view.Camera3_Frame(center, radius);
            return;
        }

        Vector2 min2 = new(float.MaxValue);
        Vector2 max2 = new(float.MinValue);
        Vector2[] corners2 = new Vector2[4];
        int count2 = 0;
        for (int i = 0; i < sel.Count; i++)
        {
            if (sel[i] is not Imp2D c2 || c2 is C2_Gizmo)
            {
                continue;
            }
            C2_Gizmo.CompCorners(c2, corners2);
            for (int k = 0; k < 4; k++)
            {
                min2 = Vector2.Min(min2, corners2[k]);
                max2 = Vector2.Max(max2, corners2[k]);
            }
            count2++;
        }
        if (count2 == 0)
        {
            min2 = Vector2.Zero;
            max2 = view.viewport2D.CanvasSize();
        }
        view.Camera2_Frame(min2, max2);
    }

    ImpComp Drop_Pick(ImpPlayer player)
    {
        if (view?.scene?.root == null)
        {
            return null;
        }
        if (view.view_mode == ESceneEditView.Mode_3D)
        {
            view.viewport3D.Trace_World(player.cursor.position, out _, out Imp3D hit);
            return hit;
        }
        return view.viewport2D.Trace_Pick(player.cursor.position);
    }

    static ImpAsset Drop_AssetOf(ImpComp dropped)
    {
        object payload = dropped?.CursorGrab_Payload();
        if (payload is ImpAsset asset)
        {
            return asset;
        }
        if (payload is string path)
        {
            return ImpAsset.Load(path);
        }
        return null;
    }

    void Drop_Bind(ImpAsset asset, ImpPlayer player)
    {
        if (_drop_asset == asset)
        {
            return;
        }
        Imp2D drop_view = view?.Drop_View();
        if (_drop_asset != null)
        {
            if (_drop_comp != null)
            {
                _drop_asset.SceneDrop_CompExit(_drop_comp, player);
                _drop_comp = null;
            }
            _drop_asset.SceneDrop_Exit(drop_view, player);
        }
        _drop_asset = asset;
        if (_drop_asset != null)
        {
            _drop_asset.SceneDrop_Enter(drop_view, player);
        }
    }

    void Drop_Tick(ImpPlayer player, float dt)
    {
        if (_drop_asset == null || player == null || !player.grab_is_active)
        {
            return;
        }
        Imp2D drop_view = view?.Drop_View();
        ImpComp hit = Drop_Pick(player);
        if (hit != _drop_comp)
        {
            if (_drop_comp != null)
            {
                _drop_asset.SceneDrop_CompExit(_drop_comp, player);
            }
            _drop_comp = hit;
            if (_drop_comp != null)
            {
                _drop_asset.SceneDrop_CompEnter(_drop_comp, player);
            }
        }
        _drop_asset.SceneDrop_Update(drop_view, dt, player);
    }

    static Type Drop_TypeOf(ImpComp dropped)
    {
        object payload = dropped?.CursorGrab_Payload();
        if (payload is Type t && typeof(ImpComp).IsAssignableFrom(t) && !t.IsAbstract)
        {
            return t;
        }
        return null;
    }

    void TypeDrop_Bind(Type type, ImpPlayer player)
    {
        if (_drop_type == type && (type == null || _drop_type_ghost != null))
        {
            return;
        }
        TypeDrop_Clear();
        _drop_type = type;
        if (type == null || view?.scene == null)
        {
            return;
        }
        ImpComp ghost;
        try
        {
            ghost = ImpComp.Create(type);
        }
        catch
        {
            ghost = null;
        }
        if (ghost == null)
        {
            return;
        }
        ghost.name = C2_Tree.Class_DisplayName(type);
        if (ghost is Imp2D)
        {
            view.view_mode = ESceneEditView.Mode_2D;
        }
        else if (ghost is Imp3D)
        {
            view.view_mode = ESceneEditView.Mode_3D;
        }
        _drop_type_ghost = ghost;
        ghost.scene = view.scene;
        Imp2D drop_view = view.Drop_View();
        if (drop_view is C2_Viewport3D v3)
        {
            v3.overlay = ghost;
        }
        else if (drop_view is C2_Viewport2D v2)
        {
            v2.overlay = ghost;
        }
        TypeDrop_Tick(player);
    }

    void TypeDrop_Clear()
    {
        Imp2D drop_view = view?.Drop_View();
        if (drop_view is C2_Viewport3D v3 && v3.overlay == _drop_type_ghost)
        {
            v3.overlay = null;
        }
        if (drop_view is C2_Viewport2D v2 && v2.overlay == _drop_type_ghost)
        {
            v2.overlay = null;
        }
        _drop_type_ghost?.Destroy();
        _drop_type_ghost = null;
        _drop_type = null;
    }

    void TypeDrop_Tick(ImpPlayer player)
    {
        if (_drop_type_ghost == null || player == null || view == null)
        {
            return;
        }
        Imp2D drop_view = view.Drop_View();
        if (_drop_type_ghost is Imp3D g3 && drop_view is C2_Viewport3D v3)
        {
            if (v3.Trace_World(player.cursor.position, out Vector3 pos, out _))
            {
                g3.Position_Set(pos, true);
            }
        }
        else if (_drop_type_ghost is Imp2D g2 && drop_view is C2_Viewport2D v2)
        {
            g2.Position_Set(v2.Trace_World(player.cursor.position), true);
        }
    }

    ImpComp TypeDrop_Commit(ImpPlayer player)
    {
        if (_drop_type_ghost == null || view == null)
        {
            return null;
        }
        ImpComp dest = view.scene?.root;
        if (dest == null || dest == _drop_type_ghost || _drop_type_ghost.IsAncestorOf(dest))
        {
            TypeDrop_Clear();
            return null;
        }
        if (dest.IsPackedForeign || dest.IsInstanceRoot)
        {
            TypeDrop_Clear();
            return null;
        }

        TTransform3? w3 = null;
        TTransform2? w2 = null;
        if (_drop_type_ghost is Imp3D c3)
        {
            w3 = c3.Transform_Get(true);
        }
        if (_drop_type_ghost is Imp2D c2)
        {
            w2 = c2.Transform_Get(true);
        }

        Imp2D drop_view = view.Drop_View();
        if (drop_view is C2_Viewport3D v3 && v3.overlay == _drop_type_ghost)
        {
            v3.overlay = null;
        }
        if (drop_view is C2_Viewport2D v2 && v2.overlay == _drop_type_ghost)
        {
            v2.overlay = null;
        }

        ImpComp spawned = _drop_type_ghost;
        _drop_type_ghost = null;
        _drop_type = null;
        spawned.name = ImpComp.Name_Unique(dest, spawned.name ?? spawned.GetType().Name);
        dest.Child_Add(spawned);
        if (w3.HasValue && spawned is Imp3D a3)
        {
            a3.Transform_Set(w3.Value, true);
        }
        if (w2.HasValue && spawned is Imp2D a2)
        {
            a2.Transform_Set(w2.Value, true);
        }
        ImpUndo.Comp_Moved(spawned, default, "Add " + spawned.name);
        return spawned;
    }

    void SelectionUndo(List<ImpComp> before, List<ImpComp> after)
    {
        ImpUndo.Push("Selection",
            () => gizmo_data.Selection_Set(before),
            () => gizmo_data.Selection_Set(after));
    }

    static List<ImpComp> SelectionRoots(List<ImpComp> comps)
    {
        List<ImpComp> roots = new();
        if (comps == null)
        {
            return roots;
        }
        for (int i = 0; i < comps.Count; i++)
        {
            ImpComp c = comps[i];
            if (c == null)
            {
                continue;
            }
            bool nested = false;
            for (ImpComp p = c.parent; p != null; p = p.parent)
            {
                if (comps.Contains(p))
                {
                    nested = true;
                    break;
                }
            }
            if (!nested)
            {
                roots.Add(c);
            }
        }
        return roots;
    }
}
