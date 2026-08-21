using System.Numerics;
using ImperiumEngine;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace Editor.Panel;

public class PNL_SceneView : C2_Box
{
    public ImpScene scene;
    
    public PNL_GameView game_view = new();
    public PNL_ScriptGraph script_graph = new();

    //one history per camera tab, so undo in one camera never reaches into another
    public ImpUndo undo = new();

    public ESceneEditorMode edit_mode = ESceneEditorMode.Mode_3D;
    public TGizmoData gizmo_data = new() { space = EGizmoSpace.Local };
    public EGizmoMode gizmo_mode
    {
        get => gizmo_data.mode;
        set => gizmo_data.mode = value;
    }
    public EGizmoSpace gizmo_orientation
    {
        get => gizmo_data.space;
        set => gizmo_data.space = value;
    }

    C3_Gizmo gizmo_3d = new();
    C2_Gizmo gizmo_2d = new();

    public C2_Viewport3D viewport3D = new()
    {
        layout = TLayout2.FULL,
        cursor_filter = ECursorFilter.Pass,
    };
    public C2_Viewport2D viewport2D = new()
    {
        layout = TLayout2.FULL,
        cursor_filter = ECursorFilter.Pass,
    };

    public C2_TabBox tabs_view = new()
    {
        layout = TLayout2.FULL,
        tab_height = 24,
        tab_width = 72,
    };

    public C2_List view_root;

    C2_List toolbar = new()
    {
        orentation = EUIOrentation.H,
        spacing = 2,
        layout = new TLayout2
        {
            size = new(0, 26),
            size_min = new(0, 26),
            orient_H = EUIViewportAlignment.Fill,
        },
    };

    //draw flags — Editor helpers on by default; G toggles
    bool view_is_debug = true;
    
    C2_EnumOption opt_edit;
    C2_EnumOption opt_gizmo;
    C2_EnumOption opt_space;
    C2_Slider snap_slider;

    enum ECaptureDrag { None, Look, Pan, Orbit, Dolly, Walk }
    ECaptureDrag drag;
    float look_dist;
    float fly_speed = 8f;
    Vector2 drag_cursor;
    bool drag_captured;
    bool skip_wrap_delta;

    bool marquee;
    bool marquee_add;
    Vector2 marquee_a, marquee_b;
    const float marquee_min = 4f;

    ImpAsset _drop_asset;
    ImpComp _drop_comp;
    Type _drop_type;
    ImpComp _drop_type_ghost;

    public bool IsCameraBusy => drag != ECaptureDrag.None;

    public PNL_SceneView()
    {
        cursor_filter = ECursorFilter.Hit;
        gizmo_3d.gizmo_data = gizmo_data;
        gizmo_2d.gizmo_data = gizmo_data;
        look_dist = Vector3.Distance(viewport3D.camera.Position, viewport3D.camera.Target);

        opt_edit = EnumOpt(typeof(ESceneEditorMode), edit_mode, e => edit_mode = (ESceneEditorMode)e);
        toolbar.Child_Add(ToolSep());
        opt_gizmo = EnumOpt(typeof(EGizmoMode), gizmo_mode, e => gizmo_mode = (EGizmoMode)e);
        toolbar.Child_Add(ToolSep());
        opt_space = EnumOpt(typeof(EGizmoSpace), gizmo_orientation, e => gizmo_orientation = (EGizmoSpace)e);
        toolbar.Child_Add(ToolSep());
        toolbar.Child_Add(new C2_Text
        {
            text = "Snap",
            style = UI_Text.MUTED,
            wrap = ETextWrap.None,
            text_alignment_h = EUIPositionAlignment.End,
            text_alignment_v = EUIPositionAlignment.Center,
            layout = new TLayout2
            {
                size = new(36, 22),
                size_min = new(32, 22),
                orient_V = EUIViewportAlignment.Center,
            },
        });
        snap_slider = new C2_Slider
        {
            is_spinner = true,
            min = 0,
            max = 0,
            value = gizmo_data.snap_translate,
            value_text_decimals = 2,
            drag_sensitivity = 0.05f,
            layout = new TLayout2
            {
                size = new(72, 22),
                size_min = new(56, 22),
                orient_V = EUIViewportAlignment.Center,
            },
        };
        snap_slider.on_changed = s =>
        {
            float v = MathF.Max(0.001f, s.value);
            if (edit_mode == ESceneEditorMode.Mode_2D)
            {
                gizmo_data.snap_translate_2d = v;
            }
            else
            {
                gizmo_data.snap_translate = v;
            }
        };
        toolbar.Child_Add(snap_slider);

        view_root = new()
        {
            name = "Scene",
            orentation = EUIOrentation.V,
            layout = new TLayout2
            {
                orient_H = EUIViewportAlignment.Fill,
                orient_V = EUIViewportAlignment.Fill,
            },
        };
        view_root.Child_Add(toolbar);
        view_root.Child_Add(viewport3D);
        view_root.Child_Add(viewport2D);
        tabs_view.Child_Add(view_root);
        tabs_view.Child_Add(game_view);
        tabs_view.Child_Add(script_graph);
        Child_Add(tabs_view);

        if (ImpPlayer.players.Count > 0)
        {
            ImpPlayer.players[0].target_focus = this;
        }
    }

    public void Camera3_Apply(Vector3 pos, Vector3 target, float fovy)
    {
        viewport3D.camera.Position = pos;
        viewport3D.camera.Target = target;
        if (fovy > 1f)
        {
            viewport3D.camera.FovY = fovy;
        }
        look_dist = Vector3.Distance(pos, target);
        if (look_dist < 0.01f)
        {
            look_dist = 0.01f;
        }
    }

    public void Camera2_Apply(Vector2 pos, float zoom)
    {
        viewport2D.camera.position = pos;
        if (zoom > 0.001f)
        {
            viewport2D.camera.zoom = zoom;
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------------------------------------------------

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        viewport3D.view_scene = scene;
        viewport2D.view_scene = scene;
        EDrawFlags view_flags = EDrawFlags.None;
        if (view_is_debug)
        {
            view_flags = EDrawFlags.Editor;
        }
        viewport3D.draw_flags = view_flags;
        viewport2D.draw_flags = view_flags;
        if (script_graph != null)
        {
            script_graph.Bind(scene);
        }

        toolbar.is_visible = true;
        viewport3D.is_visible = edit_mode == ESceneEditorMode.Mode_3D;
        viewport2D.is_visible = edit_mode == ESceneEditorMode.Mode_2D;
        opt_edit.selected_enum = edit_mode;
        opt_gizmo.selected_enum = gizmo_mode;
        opt_space.selected_enum = gizmo_orientation;

        if (edit_mode == ESceneEditorMode.Mode_2D && viewport2D.Root_Get() != null)
        {
            Imp2D.SceneLayout_Set(viewport2D.Root_Get(), viewport2D.CanvasSize());
        }

        if (snap_slider != null && !snap_slider.IsBusy)
        {
            bool two = edit_mode == ESceneEditorMode.Mode_2D;
            snap_slider.value_text_decimals = two ? 0 : 2;
            snap_slider.step = two ? 1f : 0f;
            snap_slider.drag_sensitivity = two ? 0.2f : 0.05f;
            if (two)
            {
                snap_slider.Value_SetQuiet(gizmo_data.snap_translate_2d);
            }
            else
            {
                snap_slider.Value_SetQuiet(gizmo_data.snap_translate);
            }
        }

        // Camera / gizmo / marquee only run while this viewport is the frontmost
        // surface. Local is_visible stays true when the Scene *main* tab is hidden
        // (C2_TabBox only flips the window), and the last layout rect still covers
        // Flow / Asset — polling that rect would steal hog and clicks.
        bool inner_other = tabs_view != null && tabs_view.selected_tab != 0;
        bool hidden_tab = !IsVisibleInTree();
        if (inner_other || hidden_tab)
        {
            if (ImpPlayer.players.Count > 0)
            {
                ImpPlayer script_player = ImpPlayer.players[0];
                if (drag != ECaptureDrag.None)
                {
                    EndDrag(script_player);
                }
                if (marquee)
                {
                    marquee = false;
                    if (script_player.input_hog == this)
                    {
                        script_player.input_hog = null;
                    }
                }
                if (hidden_tab)
                {
                    if (_drop_asset != null)
                    {
                        Drop_Bind(null, script_player);
                    }
                    if (script_player.input_hog == this)
                    {
                        script_player.input_hog = null;
                    }
                    if (script_player.target_focus == this)
                    {
                        script_player.target_focus = null;
                    }
                }
            }
            return;
        }

        if (ImpPlayer.players.Count == 0)
        {
            return;
        }
        ImpPlayer player = ImpPlayer.players[0];

        if (player.target_focus is C2_TextEdit te && te.is_focused && ImpPlayer.Target_IsLive(te))
        {
            return;
        }

        bool over = player.Cursor_IsInDimensions(ActiveViewDim());
        bool lmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left);
        bool rmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Right);
        bool mmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Middle);
        bool alt = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftAlt) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightAlt);
        bool shift = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftShift) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightShift);
        bool lmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left);
        bool rmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Right);
        bool mmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Middle);
        bool ui_block = player.target_cursor is C2_Seperator
            || player.input_hog is C2_Seperator
            || player.grab_is_active;

        if (over && !ui_block && (lmb_p || rmb_p || mmb_p))
        {
            player.target_focus = this;
        }

        bool ours = player.target_focus == this || IsChildFocus(player.target_focus);
        if (!over && !ours)
        {
            return;
        }

        bool hogged = player.input_hog != null && player.input_hog != this;
        bool giz_drag = gizmo_3d.is_dragging || gizmo_2d.is_dragging;
        if (hogged && drag == ECaptureDrag.None && !giz_drag)
        {
            return;
        }

        gizmo_data.snap_active = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl)
            || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);

        if (!IsCameraBusy)
        {
            if (ImpPlayer.Key_IsPressed(EInputKey.Key_W))
            {
                gizmo_mode = EGizmoMode.Translate;
            }
            if (ImpPlayer.Key_IsPressed(EInputKey.Key_E))
            {
                gizmo_mode = EGizmoMode.Rotate;
            }
            if (ImpPlayer.Key_IsPressed(EInputKey.Key_R))
            {
                gizmo_mode = EGizmoMode.Scale;
            }
        }
        if (!IsCameraBusy && ImpPlayer.Key_IsPressed(EInputKey.Key_F))
        {
            Focus_Selection();
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_T))
        {
            if (gizmo_orientation == EGizmoSpace.Local)
            {
                gizmo_orientation = EGizmoSpace.World;
            }
            else
            {
                gizmo_orientation = EGizmoSpace.Local;
            }
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Tab))
        {
            if (edit_mode == ESceneEditorMode.Mode_3D)
            {
                edit_mode = ESceneEditorMode.Mode_2D;
            }
            else
            {
                edit_mode = ESceneEditorMode.Mode_3D;
            }
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_1))
        {
            edit_mode = ESceneEditorMode.Mode_3D;
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_2))
        {
            edit_mode = ESceneEditorMode.Mode_2D;
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Escape))
        {
            gizmo_data.Selection_Clear();
        }
        if (!IsCameraBusy && ImpPlayer.Key_IsPressed(EInputKey.Key_G))
        {
            view_is_debug = !view_is_debug;
        }

        if (drag == ECaptureDrag.Look && lmb)
        {
            drag = ECaptureDrag.Walk;
        }
        if (drag == ECaptureDrag.Walk && !lmb && rmb)
        {
            drag = ECaptureDrag.Look;
        }

        if (drag == ECaptureDrag.Look && !rmb)
        {
            EndDrag(player);
        }
        else if (drag == ECaptureDrag.Pan)
        {
            bool pan_held = mmb || (edit_mode == ESceneEditorMode.Mode_2D && (rmb || (alt && lmb)));
            if (!pan_held)
            {
                EndDrag(player);
            }
        }
        else if (drag == ECaptureDrag.Orbit && !lmb)
        {
            EndDrag(player);
        }
        else if (drag == ECaptureDrag.Dolly && !rmb)
        {
            EndDrag(player);
        }
        else if (drag == ECaptureDrag.Walk && (!lmb || !rmb))
        {
            EndDrag(player);
        }

        if (drag != ECaptureDrag.None)
        {
            UpdateCamera(dt, player);
            return;
        }

        TDimensions2 vp = ActiveViewDim();
        bool allow_gizmo = over && ours && !alt && !ui_block;
        bool giz_busy = false;
        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            giz_busy = gizmo_3d.Interact(viewport3D.camera, vp, player, this, allow_gizmo);
        }
        else
        {
            giz_busy = gizmo_2d.Interact(viewport2D.camera, vp, player, this, allow_gizmo);
        }

        if (marquee)
        {
            marquee_b = player.cursor.position;
            if (edit_mode == ESceneEditorMode.Mode_3D && rmb)
            {
                marquee = false;
                BeginDrag(player, ECaptureDrag.Walk, true);
                return;
            }
            if (!lmb)
            {
                marquee = false;
                if (player.input_hog == this)
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
            player.input_hog = this;
            return;
        }

        if (!giz_busy && ours && over && !hogged && !ui_block)
        {
            if (edit_mode == ESceneEditorMode.Mode_3D)
            {
                if (alt && lmb_p)
                {
                    BeginDrag(player, ECaptureDrag.Orbit, true);
                }
                else if (alt && rmb_p)
                {
                    BeginDrag(player, ECaptureDrag.Dolly, true);
                }
                else if (mmb_p)
                {
                    BeginDrag(player, ECaptureDrag.Pan, true);
                }
                else if (lmb && rmb && (lmb_p || rmb_p))
                {
                    BeginDrag(player, ECaptureDrag.Walk, true);
                }
                else if (rmb_p)
                {
                    BeginDrag(player, ECaptureDrag.Look, true);
                }
            }
            else
            {
                if (mmb_p || rmb_p || (alt && lmb_p))
                {
                    BeginDrag(player, ECaptureDrag.Pan, false);
                }
            }
        }

        if (player.grab_is_active && over)
        {
            Drop_Tick(player, (float)dt);
            TypeDrop_Tick(player);
        }

        if (drag == ECaptureDrag.None && over && !hogged && !ui_block)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                if (edit_mode == ESceneEditorMode.Mode_3D)
                {
                    Vector3 fwd = CamFwd();
                    look_dist = Math.Clamp(look_dist * MathF.Pow(0.85f, wheel), 0.2f, 10000f);
                    viewport3D.camera.Position = viewport3D.camera.Target - fwd * look_dist;
                }
                else
                {
                    Vector2 before = ImpGizmo.ScreenToWorld(player.cursor.position, viewport2D.camera, vp);
                    viewport2D.camera.zoom = Math.Clamp(viewport2D.camera.zoom * MathF.Pow(1.18f, wheel), 0.05f, 32f);
                    Vector2 after = ImpGizmo.ScreenToWorld(player.cursor.position, viewport2D.camera, vp);
                    viewport2D.camera.position += before - after;
                }
            }
        }
    }

    public override void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt)
    {
        base._Notify_AsCursorTarget(player, notify, dt);
        if (notify == ENotifyGeneric.Update)
        {
            Drop_Tick(player, (float)dt);
            TypeDrop_Tick(player);
        }
    }

    public override void _Notify_OnGrabDrop(ImpPlayer player, ENotifyGrabTarget notify, ImpComp other, double dt)
    {
        base._Notify_OnGrabDrop(player, notify, other, dt);
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
                ImpComp spawned = _drop_asset.SceneDrop_DropOnComp(scene?.root, player);
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

    // ---------------------------------------------------------------------------------------------------------
    // Draw overlays
    // ---------------------------------------------------------------------------------------------------------

    public override void OnDraw2DForeground(double dt, EDrawFlags flags)
    {
        base.OnDraw2DForeground(dt, flags);
        if (tabs_view != null && tabs_view.selected_tab != 0)
        {
            return;
        }
        TDimensions2 vp = ActiveViewDim();
        if (vp.size.X <= 0 || vp.size.Y <= 0)
        {
            return;
        }

        Clip_Push(vp);
        if (edit_mode == ESceneEditorMode.Mode_2D)
        {
            DrawGrid2D(vp);
            DrawCanvasEdge(vp);
        }

        double t_giz = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            gizmo_3d.Draw(viewport3D.camera, vp, viewport3D.Root_Get());
        }
        else
        {
            gizmo_2d.Draw(viewport2D.camera, vp);
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
        Clip_Pop();
    }

    // ---------------------------------------------------------------------------------------------------------
    // Pick / focus
    // ---------------------------------------------------------------------------------------------------------

    void PickAt(Vector2 screen, bool additive)
    {
        if (scene?.root == null)
        {
            return;
        }
        ImpComp hit;
        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            hit = Imp3D.Select(viewport3D.Root_Get(), viewport3D.Trace_Ray(screen), out _);
        }
        else
        {
            hit = viewport2D.Trace_Pick(screen);
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
        if (scene?.root == null)
        {
            return;
        }
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        List<ImpComp> hits = new();
        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            C3_Gizmo.PickRect(scene.root, viewport3D.camera, vp, min, max, hits);
        }
        else
        {
            C2_Gizmo.PickRect(scene.root, viewport2D.camera, vp, min, max, hits);
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

    public void Focus_Selection()
    {
        List<ImpComp> sel = gizmo_data.selected_comps;
        TDimensions2 vp = ActiveViewDim();

        if (edit_mode == ESceneEditorMode.Mode_3D)
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
            float half_fov = viewport3D.camera.FovY * 0.5f * (MathF.PI / 180f);
            float sin = MathF.Max(0.05f, MathF.Sin(half_fov));
            look_dist = Math.Clamp(radius / sin * 1.2f, 0.5f, 10000f);
            viewport3D.camera.Target = center;
            viewport3D.camera.Position = center - CamFwd() * look_dist;
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
            max2 = viewport2D.CanvasSize();
        }
        Vector2 span = Vector2.Max(max2 - min2, new Vector2(16f)) * 1.15f;
        viewport2D.camera.position = (min2 + max2) * 0.5f;
        float zoom = MathF.Min(
            MathF.Max(1f, vp.size.X) / span.X,
            MathF.Max(1f, vp.size.Y) / span.Y);
        viewport2D.camera.zoom = Math.Clamp(zoom, 0.05f, 32f);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Camera
    // ---------------------------------------------------------------------------------------------------------

    void UpdateCamera(double dt, ImpPlayer player)
    {
        if (look_dist < 0.05f)
        {
            look_dist = Vector3.Distance(viewport3D.camera.Position, viewport3D.camera.Target);
        }

        Vector2 md = Vector2.Zero;
        if (!skip_wrap_delta)
        {
            md = Raylib.GetMouseDelta();
        }
        skip_wrap_delta = false;

        if (edit_mode == ESceneEditorMode.Mode_2D)
        {
            float z = viewport2D.camera.zoom;
            if (z <= 1e-6f)
            {
                z = 1f;
            }
            viewport2D.camera.position += new Vector2(-md.X, -md.Y) / z;
            WrapCursor();
            return;
        }

        Vector3 fwd = CamFwd();
        Vector3 right = CamRight(fwd);
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, fwd));

        float GetYaw() => MathF.Atan2(fwd.X, fwd.Z);
        float GetPitch() => MathF.Asin(Math.Clamp(fwd.Y, -1f, 1f));
        void SetView(float yaw, float pitch, bool orbit)
        {
            pitch = Math.Clamp(pitch, -1.53f, 1.53f);
            fwd = new Vector3(
                MathF.Cos(pitch) * MathF.Sin(yaw),
                MathF.Sin(pitch),
                MathF.Cos(pitch) * MathF.Cos(yaw));
            if (orbit)
            {
                viewport3D.camera.Position = viewport3D.camera.Target - fwd * look_dist;
            }
            else
            {
                viewport3D.camera.Target = viewport3D.camera.Position + fwd * look_dist;
            }
            right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
            if (right.LengthSquared() < 1e-8f)
            {
                right = Vector3.UnitX;
            }
            up = Vector3.Normalize(Vector3.Cross(right, fwd));
        }

        const float look_sens = 0.0045f;
        const float pan_sens = 0.0018f;

        if (drag == ECaptureDrag.Look)
        {
            SetView(GetYaw() - md.X * look_sens, GetPitch() - md.Y * look_sens, false);
            Vector3 move = ImpPlayer.Action_GetAxis("_Move");
            float vert = 0f;
            if (ImpPlayer.Key_IsHeld(EInputKey.Key_E))
            {
                vert += 1f;
            }
            if (ImpPlayer.Key_IsHeld(EInputKey.Key_Q))
            {
                vert -= 1f;
            }
            float speed = fly_speed;
            if (ImpPlayer.Key_IsHeld(EInputKey.Key_LeftShift) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightShift))
            {
                speed *= 4f;
            }
            Vector3 delta = (fwd * move.X + right * move.Z + Vector3.UnitY * vert) * speed * (float)dt;
            viewport3D.camera.Position += delta;
            viewport3D.camera.Target += delta;

            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                fly_speed = Math.Clamp(fly_speed * MathF.Pow(1.25f, wheel), 0.25f, 256f);
            }
        }
        else if (drag == ECaptureDrag.Orbit)
        {
            SetView(GetYaw() - md.X * look_sens, GetPitch() - md.Y * look_sens, true);
        }
        else if (drag == ECaptureDrag.Pan)
        {
            Vector3 delta = (-right * md.X + up * md.Y) * look_dist * pan_sens;
            viewport3D.camera.Position += delta;
            viewport3D.camera.Target += delta;
        }
        else if (drag == ECaptureDrag.Dolly || drag == ECaptureDrag.Walk)
        {
            float side = 0f;
            if (drag == ECaptureDrag.Walk)
            {
                side = md.X;
            }
            Vector3 delta = (fwd * -md.Y + right * side) * look_dist * pan_sens;
            viewport3D.camera.Position += delta;
            viewport3D.camera.Target += delta;
            look_dist = Vector3.Distance(viewport3D.camera.Position, viewport3D.camera.Target);
        }

        WrapCursor();
    }

    Vector3 CamFwd()
    {
        Vector3 fwd = viewport3D.camera.Target - viewport3D.camera.Position;
        if (fwd.LengthSquared() < 1e-8f)
        {
            return -Vector3.UnitZ;
        }
        return Vector3.Normalize(fwd);
    }

    static Vector3 CamRight(Vector3 fwd)
    {
        Vector3 right = Vector3.Cross(fwd, Vector3.UnitY);
        if (right.LengthSquared() < 1e-8f)
        {
            return Vector3.UnitX;
        }
        return Vector3.Normalize(right);
    }

    void WrapCursor()
    {
        if (drag == ECaptureDrag.None || !drag_captured)
        {
            return;
        }
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mp = Raylib.GetMousePosition();
        if (mp.X <= 1 || mp.Y <= 1 || mp.X >= sw - 2 || mp.Y >= sh - 2)
        {
            Raylib.SetMousePosition(sw / 2, sh / 2);
            skip_wrap_delta = true;
        }
    }

    void BeginDrag(ImpPlayer player, ECaptureDrag mode, bool capture)
    {
        drag = mode;
        drag_captured = capture;
        drag_cursor = player.cursor.position;
        player.target_focus = this;
        player.input_hog = this;
        if (capture)
        {
            player.cursor.is_hidden = true;
        }
    }

    void EndDrag(ImpPlayer player)
    {
        drag = ECaptureDrag.None;
        if (player.input_hog == this)
        {
            player.input_hog = null;
        }
        if (!drag_captured)
        {
            drag_captured = false;
            return;
        }
        drag_captured = false;
        player.cursor.is_hidden = false;
        Raylib.SetMousePosition((int)drag_cursor.X, (int)drag_cursor.Y);
        player.cursor.position = drag_cursor;
        skip_wrap_delta = true;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Drop
    // ---------------------------------------------------------------------------------------------------------

    ImpComp Drop_Pick(ImpPlayer player)
    {
        if (scene?.root == null)
        {
            return null;
        }
        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            viewport3D.Trace_World(player.cursor.position, out _, out Imp3D hit);
            return hit;
        }
        return viewport2D.Trace_Pick(player.cursor.position);
    }

    Imp2D Drop_View()
    {
        if (edit_mode == ESceneEditorMode.Mode_2D)
        {
            return viewport2D;
        }
        return viewport3D;
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
        Imp2D view = Drop_View();
        if (_drop_asset != null)
        {
            if (_drop_comp != null)
            {
                _drop_asset.SceneDrop_CompExit(_drop_comp, player);
                _drop_comp = null;
            }
            _drop_asset.SceneDrop_Exit(view, player);
        }
        _drop_asset = asset;
        if (_drop_asset != null)
        {
            _drop_asset.SceneDrop_Enter(view, player);
        }
    }

    void Drop_Tick(ImpPlayer player, float dt)
    {
        if (_drop_asset == null || !player.grab_is_active)
        {
            return;
        }
        Imp2D view = Drop_View();
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
        _drop_asset.SceneDrop_Update(view, dt, player);
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
        if (type == null || scene == null)
        {
            return;
        }
        ImpComp ghost;
        try
        {
            ghost = Activator.CreateInstance(type) as ImpComp;
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
            edit_mode = ESceneEditorMode.Mode_2D;
        }
        else if (ghost is Imp3D)
        {
            edit_mode = ESceneEditorMode.Mode_3D;
        }
        _drop_type_ghost = ghost;
        ghost.scene = scene;
        Imp2D view = Drop_View();
        if (view is C2_Viewport3D v3)
        {
            v3.overlay = ghost;
        }
        else if (view is C2_Viewport2D v2)
        {
            v2.overlay = ghost;
        }
        TypeDrop_Tick(player);
    }

    void TypeDrop_Clear()
    {
        Imp2D view = Drop_View();
        if (view is C2_Viewport3D v3 && v3.overlay == _drop_type_ghost)
        {
            v3.overlay = null;
        }
        if (view is C2_Viewport2D v2 && v2.overlay == _drop_type_ghost)
        {
            v2.overlay = null;
        }
        _drop_type_ghost?.Destroy();
        _drop_type_ghost = null;
        _drop_type = null;
    }

    void TypeDrop_Tick(ImpPlayer player)
    {
        if (_drop_type_ghost == null || player == null)
        {
            return;
        }
        Imp2D view = Drop_View();
        if (_drop_type_ghost is Imp3D g3 && view is C2_Viewport3D v3)
        {
            if (v3.Trace_World(player.cursor.position, out Vector3 pos, out _))
            {
                g3.Position_Set(pos, true);
            }
        }
        else if (_drop_type_ghost is Imp2D g2 && view is C2_Viewport2D v2)
        {
            g2.Position_Set(v2.Trace_World(player.cursor.position), true);
        }
    }

    ImpComp TypeDrop_Commit(ImpPlayer player)
    {
        if (_drop_type_ghost == null)
        {
            return null;
        }
        ImpComp dest = scene?.root;
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

        Imp2D view = Drop_View();
        if (view is C2_Viewport3D v3 && v3.overlay == _drop_type_ghost)
        {
            v3.overlay = null;
        }
        if (view is C2_Viewport2D v2 && v2.overlay == _drop_type_ghost)
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

    // ---------------------------------------------------------------------------------------------------------
    // Overlay helpers
    // ---------------------------------------------------------------------------------------------------------

    TDimensions2 ActiveViewDim()
    {
        if (edit_mode == ESceneEditorMode.Mode_2D)
        {
            return viewport2D.Dimensions_Get();
        }
        return viewport3D.Dimensions_Get();
    }

    bool IsChildFocus(ImpComp focus)
    {
        ImpComp n = focus;
        while (n != null)
        {
            if (n == this)
            {
                return true;
            }
            n = n.parent;
        }
        return false;
    }

    void DrawCanvasEdge(TDimensions2 vp)
    {
        Vector2 canvas = viewport2D.CanvasSize();
        Vector2 a = ImpGizmo.WorldToScreen(Vector2.Zero, viewport2D.camera, vp);
        Vector2 b = ImpGizmo.WorldToScreen(new Vector2(canvas.X, 0), viewport2D.camera, vp);
        Vector2 c = ImpGizmo.WorldToScreen(canvas, viewport2D.camera, vp);
        Vector2 d = ImpGizmo.WorldToScreen(new Vector2(0, canvas.Y), viewport2D.camera, vp);
        Color edge = new(70, 160, 255, 220);
        Raylib.DrawLineEx(a, b, 2f, edge);
        Raylib.DrawLineEx(b, c, 2f, edge);
        Raylib.DrawLineEx(c, d, 2f, edge);
        Raylib.DrawLineEx(d, a, 2f, edge);
    }

    void DrawGrid2D(TDimensions2 vp)
    {
        float z = viewport2D.camera.zoom;
        if (z <= 1e-6f)
        {
            z = 1f;
        }
        float step = 8f;
        if (gizmo_data.snap_translate_2d > 1f)
        {
            step = gizmo_data.snap_translate_2d;
        }
        if (step < 1f)
        {
            step = 1f;
        }
        while (step * z < 8f)
        {
            step *= 2f;
        }
        while (step * z > 96f && step > 1f)
        {
            step *= 0.5f;
        }

        Vector2 min = ImpGizmo.ScreenToWorld(vp.position, viewport2D.camera, vp);
        Vector2 max = ImpGizmo.ScreenToWorld(vp.position + vp.size, viewport2D.camera, vp);
        float x0 = MathF.Floor(MathF.Min(min.X, max.X) / step) * step;
        float y0 = MathF.Floor(MathF.Min(min.Y, max.Y) / step) * step;
        float x1 = MathF.Max(min.X, max.X);
        float y1 = MathF.Max(min.Y, max.Y);

        Color minor = new(255, 255, 255, 16);
        Color major = new(255, 255, 255, 36);
        Color axis = new(255, 255, 255, 70);
        float major_every = step * 8f;

        for (float x = x0; x <= x1; x += step)
        {
            Vector2 a = ImpGizmo.WorldToScreen(new Vector2(x, min.Y), viewport2D.camera, vp);
            Vector2 b = ImpGizmo.WorldToScreen(new Vector2(x, max.Y), viewport2D.camera, vp);
            Color col = minor;
            if (MathF.Abs(x) < 0.01f)
            {
                col = axis;
            }
            else if (MathF.Abs(x / major_every - MathF.Round(x / major_every)) < 0.01f)
            {
                col = major;
            }
            Raylib.DrawLineEx(a, b, 1f, col);
        }
        for (float y = y0; y <= y1; y += step)
        {
            Vector2 a = ImpGizmo.WorldToScreen(new Vector2(min.X, y), viewport2D.camera, vp);
            Vector2 b = ImpGizmo.WorldToScreen(new Vector2(max.X, y), viewport2D.camera, vp);
            Color col = minor;
            if (MathF.Abs(y) < 0.01f)
            {
                col = axis;
            }
            else if (MathF.Abs(y / major_every - MathF.Round(y / major_every)) < 0.01f)
            {
                col = major;
            }
            Raylib.DrawLineEx(a, b, 1f, col);
        }
    }

    C2_EnumOption EnumOpt(Type type, Enum value, Action<Enum> change)
    {
        C2_EnumOption e = new()
        {
            base_enum = type,
            selected_enum = value,
            on_change = change,
            show_name = true,
            show_icon = true,
            orentation = EUIOrentation.H,
            layout = new TLayout2
            {
                size = new(48, 22),
                size_min = new(22, 22),
                orient_V = EUIViewportAlignment.Center,
            },
        };
        toolbar.Child_Add(e);
        return e;
    }

    static C2_Seperator ToolSep()
    {
        return new C2_Seperator
        {
            orentation = EUIOrentation.H,
            is_draggable = false,
            thickness = 8,
            layout = new TLayout2
            {
                size = new(8, 22),
                orient_V = EUIViewportAlignment.Fill,
            },
        };
    }
}
