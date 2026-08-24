using System.Numerics;
using Editor.EditMode;
using ImperiumEngine;
using ImperiumEngine.Comps._2D;
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

    public ESceneEditView view_mode = ESceneEditView.Mode_3D;
    public ESceneEditMode edit_mode = ESceneEditMode.Comps;
    public EditMode_Comp mode_comp = new();
    public EditMode_Landscape mode_land = new();
    public EdEditMode mode;

    public TGizmoData gizmo_data => mode_comp.gizmo_data;
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
    
    C2_EnumOption opt_view;
    C2_EnumOption opt_mode;
    C2_EnumOption opt_gizmo;
    C2_EnumOption opt_space;
    C2_Slider snap_slider;
    C2_Text snap_label;
    C2_Seperator sep_gizmo;
    C2_Seperator sep_space;
    C2_Seperator sep_snap;

    enum ECaptureDrag { None, Look, Pan, Orbit, Dolly, Walk }
    ECaptureDrag drag;
    float look_dist;
    float fly_speed = 8f;
    Vector2 drag_cursor;
    bool drag_captured;
    bool skip_wrap_delta;

    public bool IsCameraBusy => drag != ECaptureDrag.None;

    public PNL_SceneView()
    {
        cursor_filter = ECursorFilter.Hit;
        mode_comp.view = this;
        mode_land.view = this;
        mode = mode_comp;
        mode.OnBegin();
        look_dist = Vector3.Distance(viewport3D.camera.Position, viewport3D.camera.Target);

        opt_view = EnumOpt(typeof(ESceneEditView), view_mode, e => view_mode = (ESceneEditView)e);
        toolbar.Child_Add(ToolSep());
        opt_mode = EnumOpt(typeof(ESceneEditMode), edit_mode, e => EditMode_Set((ESceneEditMode)e));
        sep_gizmo = ToolSep();
        toolbar.Child_Add(sep_gizmo);
        opt_gizmo = EnumOpt(typeof(EGizmoMode), gizmo_mode, e => gizmo_mode = (EGizmoMode)e);
        sep_space = ToolSep();
        toolbar.Child_Add(sep_space);
        opt_space = EnumOpt(typeof(EGizmoSpace), gizmo_orientation, e => gizmo_orientation = (EGizmoSpace)e);
        sep_snap = ToolSep();
        toolbar.Child_Add(sep_snap);
        snap_label = new C2_Text
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
        };
        toolbar.Child_Add(snap_label);
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
            if (view_mode == ESceneEditView.Mode_2D)
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

    public void Camera3_Frame(Vector3 center, float radius)
    {
        float half_fov = viewport3D.camera.FovY * 0.5f * (MathF.PI / 180f);
        float sin = MathF.Max(0.05f, MathF.Sin(half_fov));
        look_dist = Math.Clamp(radius / sin * 1.2f, 0.5f, 10000f);
        viewport3D.camera.Target = center;
        viewport3D.camera.Position = center - CamFwd() * look_dist;
    }

    public void Camera2_Frame(Vector2 min, Vector2 max)
    {
        TDimensions2 vp = ActiveViewDim();
        Vector2 span = Vector2.Max(max - min, new Vector2(16f)) * 1.15f;
        viewport2D.camera.position = (min + max) * 0.5f;
        float zoom = MathF.Min(
            MathF.Max(1f, vp.size.X) / span.X,
            MathF.Max(1f, vp.size.Y) / span.Y);
        viewport2D.camera.zoom = Math.Clamp(zoom, 0.05f, 32f);
    }

    public void Camera_BeginWalk(ImpPlayer player)
    {
        BeginDrag(player, ECaptureDrag.Walk, true);
    }

    public void EditMode_Set(ESceneEditMode next)
    {
        EdEditMode next_mode = mode_comp;
        if (next == ESceneEditMode.Landscape)
        {
            next_mode = mode_land;
        }
        if (edit_mode == next && mode == next_mode)
        {
            return;
        }
        edit_mode = next;
        if (mode == next_mode)
        {
            return;
        }
        if (mode != null)
        {
            mode.OnEnd();
        }
        mode = next_mode;
        if (mode != null)
        {
            mode.view = this;
            mode.OnBegin();
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
        viewport3D.is_visible = view_mode == ESceneEditView.Mode_3D;
        viewport2D.is_visible = view_mode == ESceneEditView.Mode_2D;
        if (opt_view != null)
        {
            opt_view.selected_enum = view_mode;
        }
        if (opt_mode != null)
        {
            opt_mode.selected_enum = edit_mode;
        }
        opt_gizmo.selected_enum = gizmo_mode;
        opt_space.selected_enum = gizmo_orientation;
        bool comps = edit_mode == ESceneEditMode.Comps;
        if (sep_gizmo != null)
        {
            sep_gizmo.is_visible = comps;
        }
        opt_gizmo.is_visible = comps;
        if (sep_space != null)
        {
            sep_space.is_visible = comps;
        }
        opt_space.is_visible = comps;
        if (sep_snap != null)
        {
            sep_snap.is_visible = comps;
        }
        if (snap_label != null)
        {
            snap_label.is_visible = comps;
        }
        snap_slider.is_visible = comps;

        if (view_mode == ESceneEditView.Mode_2D && viewport2D.Root_Get() != null)
        {
            Imp2D.SceneLayout_Set(viewport2D.Root_Get(), viewport2D.CanvasSize());
        }

        if (snap_slider != null && !snap_slider.IsBusy)
        {
            bool two = view_mode == ESceneEditView.Mode_2D;
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
                if (mode != null)
                {
                    mode.OnHidden();
                }
                if (hidden_tab)
                {
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
        bool mode_busy = mode != null && mode.IsBusy;
        if (hogged && drag == ECaptureDrag.None && !mode_busy)
        {
            return;
        }

        if (ImpPlayer.Key_IsPressed(EInputKey.Key_Tab))
        {
            if (view_mode == ESceneEditView.Mode_3D)
            {
                view_mode = ESceneEditView.Mode_2D;
            }
            else
            {
                view_mode = ESceneEditView.Mode_3D;
            }
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_1))
        {
            view_mode = ESceneEditView.Mode_3D;
        }
        if (ImpPlayer.Key_IsPressed(EInputKey.Key_2))
        {
            view_mode = ESceneEditView.Mode_2D;
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
            bool pan_held = mmb || (view_mode == ESceneEditView.Mode_2D && (rmb || (alt && lmb)));
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

        if (mode != null)
        {
            mode.OnUpdate(dt);
        }
        if (drag != ECaptureDrag.None)
        {
            UpdateCamera(dt, player);
            return;
        }

        bool block_cam = mode != null && mode.BlocksCamera;
        if (!block_cam && ours && over && !hogged && !ui_block)
        {
            if (view_mode == ESceneEditView.Mode_3D)
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

        bool block_wheel = mode != null && mode.BlocksWheel;
        if (drag == ECaptureDrag.None && over && !hogged && !ui_block && !block_wheel)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                if (view_mode == ESceneEditView.Mode_3D)
                {
                    Vector3 fwd = CamFwd();
                    look_dist = Math.Clamp(look_dist * MathF.Pow(0.85f, wheel), 0.2f, 10000f);
                    viewport3D.camera.Position = viewport3D.camera.Target - fwd * look_dist;
                }
                else
                {
                    TDimensions2 vp = ActiveViewDim();
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
        if (notify == ENotifyGeneric.Update && mode != null)
        {
            mode.OnCursorUpdate(player, dt);
        }
    }

    public override void _Notify_OnGrabDrop(ImpPlayer player, ENotifyGrabTarget notify, ImpComp other, double dt)
    {
        base._Notify_OnGrabDrop(player, notify, other, dt);
        if (mode != null)
        {
            mode.OnGrabDrop(player, notify, other, dt);
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
        if (view_mode == ESceneEditView.Mode_2D)
        {
            DrawGrid2D(vp);
            DrawCanvasEdge(vp);
        }

        if (mode != null)
        {
            mode.OnDraw2DForeground(dt, flags);
        }
        Clip_Pop();
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

        if (view_mode == ESceneEditView.Mode_2D)
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

            Vector3 delta = (fwd * -move.Z + right * move.X + Vector3.UnitY * vert) * speed * (float)dt;
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

    public Imp2D Drop_View()
    {
        if (view_mode == ESceneEditView.Mode_2D)
        {
            return viewport2D;
        }
        return viewport3D;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Overlay helpers
    // ---------------------------------------------------------------------------------------------------------

    public TDimensions2 ActiveViewDim()
    {
        if (view_mode == ESceneEditView.Mode_2D)
        {
            return viewport2D.Dimensions_Get();
        }
        return viewport3D.Dimensions_Get();
    }

    public bool IsChildFocus(ImpComp focus)
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
