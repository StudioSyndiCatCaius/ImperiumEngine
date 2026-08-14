using System.Numerics;
using ImperiumEngine.Comps._3D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using R3D_cs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_SceneView : ImpComp2D
{
    public ImpScene? scene;
    public ImpComp drop_preview;
    public TGizmoData gizmo_data;
    public C3_Gizmo gizmo_3d;
    public C2_Gizmo gizmo_2d;
    public ESceneEditorMode edit_mode = ESceneEditorMode.Mode_3D;
    public TCamera2D camera_2d = new() { position = new Vector2(960f, 540f), zoom = 0.5f };

    public Camera3D camera = new()
    {
        Position = new Vector3(6.5f, 4.5f, 8.5f),
        Target = new Vector3(0f, 0.5f, 0f),
        Up = Vector3.UnitY,
        FovY = 50f,
        Projection = CameraProjection.Perspective,
    };

    RenderTexture2D target;
    int target_w;
    int target_h;

    enum ECaptureDrag { None, Look, Pan, Orbit, Dolly, Walk }
    ECaptureDrag drag;
    float look_dist;
    float fly_speed = 8f;
    Vector2 drag_cursor;
    bool drag_captured; //true when the drag hid + wrapped the cursor (3D style capture)
    bool skip_wrap_delta;

    bool marquee;
    bool marquee_add;
    Vector2 marquee_a, marquee_b;
    const float marquee_min = 4f;

    public C2_SceneView()
    {
        view_alighnment_H = EUIViewportAlignment.Fill;
        view_alighnment_V = EUIViewportAlignment.Fill;
        cursor_filter = ECursorFilter.Hit;
        look_dist = Vector3.Distance(camera.Position, camera.Target);
    }

    public bool IsCameraBusy => drag != ECaptureDrag.None;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        // Layout rules for scene content have to be in force for update, hit test and draw
        // alike, otherwise the gizmo and the drawn box disagree.
        if (edit_mode == ESceneEditorMode.Mode_2D && scene?.root != null)
            ImpComp2D.SceneLayout_Set(scene.root, CanvasSize());

        if (ImpPlayer.players.Count == 0) return;
        ImpPlayer player = ImpPlayer.players[0];

        if (!is_visible)
        {
            if (drag != ECaptureDrag.None) EndDrag(player);
            if (marquee)
            {
                marquee = false;
                if (player.input_hog == this) player.input_hog = null;
            }
            if (_drop_asset != null) Drop_Bind(null, player);
            return;
        }

        bool over = player.Cursor_IsInDimensions(Dimensions_Get());
        bool lmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left);
        bool rmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Right);
        bool mmb = ImpPlayer.Key_IsHeld(EInputKey.Mouse_Middle);
        bool alt = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftAlt) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightAlt);
        bool shift = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftShift) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightShift);
        bool lmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left);
        bool rmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Right);
        bool mmb_p = ImpPlayer.Key_IsPressed(EInputKey.Mouse_Middle);
        bool ui_block = player.cursor_target is C2_Seperator or C2_TreeRow
            || player.input_hog is C2_Seperator
            || player.grab_is_active;

        if (over && !ui_block && (lmb_p || rmb_p || mmb_p))
            player.ui_focus = this;

        bool focused = player.ui_focus == this;
        bool hogged = player.input_hog != null && player.input_hog != this;
        bool giz_drag = (gizmo_3d != null && gizmo_3d.is_dragging) || (gizmo_2d != null && gizmo_2d.is_dragging);
        if (hogged && drag == ECaptureDrag.None && !giz_drag) return;

        if (gizmo_data != null)
        {
            gizmo_data.snap_active = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl)
                || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);
        }

        if (drag == ECaptureDrag.Look && lmb) drag = ECaptureDrag.Walk;
        if (drag == ECaptureDrag.Walk && !lmb && rmb) drag = ECaptureDrag.Look;

        if (drag == ECaptureDrag.Look && !rmb) EndDrag(player);
        else if (drag == ECaptureDrag.Pan)
        {
            bool pan_held = mmb || (edit_mode == ESceneEditorMode.Mode_2D && (rmb || (alt && lmb)));
            if (!pan_held) EndDrag(player);
        }
        else if (drag == ECaptureDrag.Orbit && !lmb) EndDrag(player);
        else if (drag == ECaptureDrag.Dolly && !rmb) EndDrag(player);
        else if (drag == ECaptureDrag.Walk && (!lmb || !rmb)) EndDrag(player);

        if (drag != ECaptureDrag.None)
        {
            UpdateCamera(dt, player, over, focused);
            return;
        }

        TDimensions2 vp = Dimensions_Get();
        bool allow_gizmo = over && focused && !alt && !ui_block;
        bool giz_busy = false;
        if (edit_mode == ESceneEditorMode.Mode_3D && gizmo_3d != null)
            giz_busy = gizmo_3d.Interact(camera, vp, player, this, allow_gizmo);
        else if (edit_mode == ESceneEditorMode.Mode_2D && gizmo_2d != null)
            giz_busy = gizmo_2d.Interact(camera_2d, vp, player, this, allow_gizmo);

        // Marquee: a press starts a box, a release either box-selects or falls back to a click pick.
        if (marquee)
        {
            marquee_b = player.cursor.position;
            if (edit_mode == ESceneEditorMode.Mode_3D && rmb)
            {
                // LMB+RMB is the fly/walk chord, not a selection box.
                marquee = false;
                BeginDrag(player, ECaptureDrag.Walk, true);
                return;
            }
            if (!lmb)
            {
                marquee = false;
                if (player.input_hog == this) player.input_hog = null;
                if (Vector2.Distance(marquee_a, marquee_b) < marquee_min) PickAt(marquee_b, marquee_add);
                else PickRect(vp, marquee_a, marquee_b, marquee_add);
            }
            return;
        }

        if (!giz_busy && over && focused && lmb_p && !alt && !ui_block)
        {
            marquee = true;
            marquee_add = shift;
            marquee_a = player.cursor.position;
            marquee_b = marquee_a;
            player.input_hog = this;
            return;
        }

        if (!giz_busy && focused && over && !hogged && !ui_block)
        {
            if (edit_mode == ESceneEditorMode.Mode_3D)
            {
                if (alt && lmb_p) BeginDrag(player, ECaptureDrag.Orbit, true);
                else if (alt && rmb_p) BeginDrag(player, ECaptureDrag.Dolly, true);
                else if (mmb_p) BeginDrag(player, ECaptureDrag.Pan, true);
                else if (lmb && rmb && (lmb_p || rmb_p)) BeginDrag(player, ECaptureDrag.Walk, true);
                else if (rmb_p) BeginDrag(player, ECaptureDrag.Look, true);
            }
            else
            {
                // 2D pans 1:1 with the pointer, so the cursor stays visible and in place.
                if (mmb_p || rmb_p || (alt && lmb_p)) BeginDrag(player, ECaptureDrag.Pan, false);
            }
        }

        if (player.grab_is_active && over) Drop_Tick(player, (float)dt);

        if (drag == ECaptureDrag.None && over && !hogged && !ui_block)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                if (edit_mode == ESceneEditorMode.Mode_3D)
                {
                    Vector3 fwd = CamFwd();
                    look_dist = Math.Clamp(look_dist * MathF.Pow(0.85f, wheel), 0.2f, 10000f);
                    camera.Position = camera.Target - fwd * look_dist;
                }
                else
                {
                    Vector2 before = ImpGizmo.ScreenToWorld(player.cursor.position, camera_2d, vp);
                    camera_2d.zoom = Math.Clamp(camera_2d.zoom * MathF.Pow(1.18f, wheel), 0.05f, 32f);
                    Vector2 after = ImpGizmo.ScreenToWorld(player.cursor.position, camera_2d, vp);
                    camera_2d.position += before - after;
                }
            }
        }
    }

    void PickAt(Vector2 screen, bool additive)
    {
        if (gizmo_data == null || scene?.root == null) return;
        ImpComp hit = null;
        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            Ray ray = ImpGizmo.ScreenToRay3(screen, camera, Dimensions_Get());
            hit = Imp3D.Pick_Comp3D(scene.root, ray, out _);
        }
        else
        {
            hit = C2_Gizmo.Pick(scene.root, camera_2d, Dimensions_Get(), screen);
        }

        if (hit == null)
        {
            if (!additive) gizmo_data.Selection_Clear();
            return;
        }
        if (additive) gizmo_data.Selection_Toggle(hit);
        else gizmo_data.Selection_Set(new[] { hit });
    }

    void PickRect(TDimensions2 vp, Vector2 a, Vector2 b, bool additive)
    {
        if (gizmo_data == null || scene?.root == null) return;
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        List<ImpComp> hits = new();
        if (edit_mode == ESceneEditorMode.Mode_3D)
            C3_Gizmo.PickRect(scene.root, camera, vp, min, max, hits);
        else
            C2_Gizmo.PickRect(scene.root, camera_2d, vp, min, max, hits);

        if (additive) gizmo_data.Selection_Add(hits);
        else gizmo_data.Selection_Set(hits);
    }

    // Frames the current selection (or the 2D canvas when nothing is selected).
    public void Focus_Selection()
    {
        if (gizmo_data == null) return;
        List<ImpComp> sel = gizmo_data.selected_comps;
        TDimensions2 vp = Dimensions_Get();

        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            Vector3 min = new(float.MaxValue);
            Vector3 max = new(float.MinValue);
            Vector3[] corners = new Vector3[8];
            int count = 0;
            for (int i = 0; i < sel.Count; i++)
            {
                if (sel[i] is not ImpComp3D c3) continue;
                Imp3D.Comp3D_WorldCorners(c3, corners);
                for (int k = 0; k < 8; k++)
                {
                    min = Vector3.Min(min, corners[k]);
                    max = Vector3.Max(max, corners[k]);
                }
                count++;
            }
            if (count == 0) return;
            Vector3 center = (min + max) * 0.5f;
            float radius = MathF.Max(0.25f, Vector3.Distance(max, min) * 0.5f);
            float half_fov = camera.FovY * 0.5f * (MathF.PI / 180f);
            float sin = MathF.Max(0.05f, MathF.Sin(half_fov));
            look_dist = Math.Clamp(radius / sin * 1.2f, 0.5f, 10000f);
            camera.Target = center;
            camera.Position = center - CamFwd() * look_dist;
            return;
        }

        Vector2 min2 = new(float.MaxValue);
        Vector2 max2 = new(float.MinValue);
        Vector2[] corners2 = new Vector2[4];
        int count2 = 0;
        for (int i = 0; i < sel.Count; i++)
        {
            if (sel[i] is not ImpComp2D c2 || c2 is C2_Gizmo) continue;
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
            max2 = CanvasSize();
        }
        Vector2 span = Vector2.Max(max2 - min2, new Vector2(16f)) * 1.15f;
        camera_2d.position = (min2 + max2) * 0.5f;
        float zoom = MathF.Min(
            MathF.Max(1f, vp.size.X) / span.X,
            MathF.Max(1f, vp.size.Y) / span.Y);
        camera_2d.zoom = Math.Clamp(zoom, 0.05f, 32f);
    }

    void UpdateCamera(double dt, ImpPlayer player, bool over, bool focused)
    {
        if (look_dist < 0.05f) look_dist = Vector3.Distance(camera.Position, camera.Target);

        Vector2 md = skip_wrap_delta ? Vector2.Zero : Raylib.GetMouseDelta();
        skip_wrap_delta = false;

        if (edit_mode == ESceneEditorMode.Mode_2D)
        {
            float z = camera_2d.zoom <= 1e-6f ? 1f : camera_2d.zoom;
            camera_2d.position += new Vector2(-md.X, -md.Y) / z;
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
            if (orbit) camera.Position = camera.Target - fwd * look_dist;
            else camera.Target = camera.Position + fwd * look_dist;
            right = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitY));
            if (right.LengthSquared() < 1e-8f) right = Vector3.UnitX;
            up = Vector3.Normalize(Vector3.Cross(right, fwd));
        }

        const float look_sens = 0.0045f;
        const float pan_sens = 0.0018f;

        if (drag == ECaptureDrag.Look)
        {
            SetView(GetYaw() - md.X * look_sens, GetPitch() - md.Y * look_sens, false);
            Vector3 move = ImpPlayer.Action_GetAxis("_Move");
            float vert = 0f;
            if (ImpPlayer.Key_IsHeld(EInputKey.Key_E)) vert += 1f;
            if (ImpPlayer.Key_IsHeld(EInputKey.Key_Q)) vert -= 1f;
            float speed = fly_speed;
            if (ImpPlayer.Key_IsHeld(EInputKey.Key_LeftShift) || ImpPlayer.Key_IsHeld(EInputKey.Key_RightShift))
                speed *= 4f;
            Vector3 delta = (fwd * move.X + right * move.Z + Vector3.UnitY * vert) * speed * (float)dt;
            camera.Position += delta;
            camera.Target += delta;

            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f) fly_speed = Math.Clamp(fly_speed * MathF.Pow(1.25f, wheel), 0.25f, 256f);
        }
        else if (drag == ECaptureDrag.Orbit)
        {
            SetView(GetYaw() - md.X * look_sens, GetPitch() - md.Y * look_sens, true);
        }
        else if (drag == ECaptureDrag.Pan)
        {
            Vector3 delta = (-right * md.X + up * md.Y) * look_dist * pan_sens;
            camera.Position += delta;
            camera.Target += delta;
        }
        else if (drag == ECaptureDrag.Dolly || drag == ECaptureDrag.Walk)
        {
            float side = drag == ECaptureDrag.Walk ? md.X : 0f;
            Vector3 delta = (fwd * -md.Y + right * side) * look_dist * pan_sens;
            camera.Position += delta;
            camera.Target += delta;
            look_dist = Vector3.Distance(camera.Position, camera.Target);
        }

        WrapCursor();
    }

    Vector3 CamFwd()
    {
        Vector3 fwd = camera.Target - camera.Position;
        if (fwd.LengthSquared() < 1e-8f) return -Vector3.UnitZ;
        return Vector3.Normalize(fwd);
    }

    static Vector3 CamRight(Vector3 fwd)
    {
        Vector3 right = Vector3.Cross(fwd, Vector3.UnitY);
        if (right.LengthSquared() < 1e-8f) return Vector3.UnitX;
        return Vector3.Normalize(right);
    }

    void WrapCursor()
    {
        if (drag == ECaptureDrag.None || !drag_captured) return;
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
        player.ui_focus = this;
        player.input_hog = this;
        if (capture) player.cursor.is_hidden = true;
    }

    void EndDrag(ImpPlayer player)
    {
        drag = ECaptureDrag.None;
        if (player.input_hog == this) player.input_hog = null;
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

    public override void OnDraw2D(double dt, WDrawFlags flags)
    {
        base.OnDraw2D(dt, flags);

        TDimensions2 dim = Dimensions_Get();
        int w = Math.Max(1, (int)MathF.Round(dim.size.X));
        int h = Math.Max(1, (int)MathF.Round(dim.size.Y));
        if (dim.size.X < 1 || dim.size.Y < 1) return;

        if (target.Id == 0 || Math.Abs(target_w - w) > 1 || Math.Abs(target_h - h) > 1)
        {
            double t_rt = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
            if (target.Id != 0) Raylib.UnloadRenderTexture(target);
            target = Raylib.LoadRenderTexture(w, h);
            target_w = w;
            target_h = h;
            if (ImpProfiler.enabled) ImpProfiler.sv_rt += ImpProfiler.Now_Ms - t_rt;
        }
        if (target.Id == 0) return;

        if (edit_mode == ESceneEditorMode.Mode_3D)
            Draw3D(dt, w, h);
        else
            Draw2D(dt, dim, w, h);

        double t_blit = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
        Raylib.DrawTexturePro(
            target.Texture,
            new Rectangle(0, 0, w, -h),
            new Rectangle(dim.position.X, dim.position.Y, dim.size.X, dim.size.Y),
            Vector2.Zero, 0f, Color.White);
        if (ImpProfiler.enabled) ImpProfiler.sv_blit += ImpProfiler.Now_Ms - t_blit;

        Imp2D.Clip_Push(dim);
        double t_giz = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
        if (edit_mode == ESceneEditorMode.Mode_3D) gizmo_3d?.Draw(camera, dim, scene?.root);
        else gizmo_2d?.Draw(camera_2d, dim);
        if (ImpProfiler.enabled) ImpProfiler.sv_gizmo += ImpProfiler.Now_Ms - t_giz;
        if (marquee && Vector2.Distance(marquee_a, marquee_b) >= marquee_min)
        {
            Vector2 min = Vector2.Min(marquee_a, marquee_b);
            Vector2 size = Vector2.Max(marquee_a, marquee_b) - min;
            Raylib.DrawRectangleV(min, size, new Color(70, 160, 255, 40));
            Raylib.DrawRectangleLinesEx(new Rectangle(min.X, min.Y, size.X, size.Y), 1f, new Color(120, 200, 255, 220));
        }
        Imp2D.Clip_Pop();
    }

    void Draw3D(double dt, int w, int h)
    {
        scene?.ApplyRenderState();

        Camera rcam = R3D.CameraFromRL(camera);
        rcam.NearPlane = 0.05f;
        rcam.FarPlane = 500f;

        View view = new()
        {
            Camera = rcam,
            Target = target,
            Viewport = new Rectangle(0, 0, w, h),
        };

        double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
        R3D.BeginPro(view);
        scene?.Draw(dt, 0);
        drop_preview?.Draw(dt, 0, 0);
        R3D.End();
        if (ImpProfiler.enabled) ImpProfiler.sv_r3d += ImpProfiler.Now_Ms - t0;
    }

    void Draw2D(double dt, TDimensions2 dim, int w, int h)
    {
        Raylib.BeginTextureMode(target);
        Color bg = new(18, 20, 24, 255);
        Raylib.ClearBackground(bg);
        Vector2 view = new(w, h);

        // Canvas fill goes down first so the grid keeps reading inside the bounds region,
        // then the bounds outline goes on top of the grid.
        Vector2 canvas = CanvasSize();
        Vector2 a = ImpGizmo.WorldToView(Vector2.Zero, camera_2d, view);
        Vector2 b = ImpGizmo.WorldToView(new Vector2(canvas.X, 0), camera_2d, view);
        Vector2 c = ImpGizmo.WorldToView(canvas, camera_2d, view);
        Vector2 d = ImpGizmo.WorldToView(new Vector2(0, canvas.Y), camera_2d, view);
        Color fill = scene != null ? ImpGizmo.WithAlpha(scene.background_color, 255) : new Color(26, 30, 36, 255);
        Raylib.DrawTriangle(a, b, c, fill);
        Raylib.DrawTriangle(a, c, b, fill);
        Raylib.DrawTriangle(a, c, d, fill);
        Raylib.DrawTriangle(a, d, c, fill);

        DrawGrid2D(view);

        Color edge = new(70, 160, 255, 220);
        Raylib.DrawLineEx(a, b, 2f, edge);
        Raylib.DrawLineEx(b, c, 2f, edge);
        Raylib.DrawLineEx(c, d, 2f, edge);
        Raylib.DrawLineEx(d, a, 2f, edge);

        if (scene?.root != null)
        {
            ImpComp2D.SceneLayout_Set(scene.root, canvas);
            ImpComp2D.SceneDraw_Begin(camera_2d, view);
            scene.root.Draw(dt, WDrawFlags.Editor, 1);
            drop_preview?.Draw(dt, WDrawFlags.Editor, 1);
            ImpComp2D.SceneDraw_End();
        }
        Raylib.EndTextureMode();
    }

    Vector2 CanvasSize()
    {
        if (scene != null && scene.canvas_size.X > 1 && scene.canvas_size.Y > 1)
            return scene.canvas_size;
        return new Vector2(1920, 1080);
    }

    void DrawGrid2D(Vector2 view)
    {
        float z = camera_2d.zoom <= 1e-6f ? 1f : camera_2d.zoom;
        float step = gizmo_data != null && gizmo_data.snap_translate_2d > 1f
            ? gizmo_data.snap_translate_2d
            : 8f;
        if (step < 1f) step = 1f;
        // Keep on-screen spacing readable at any zoom (and the line count bounded).
        while (step * z < 8f) step *= 2f;
        while (step * z > 96f && step > 1f) step *= 0.5f;

        Vector2 min = ImpGizmo.ViewToWorld(Vector2.Zero, camera_2d, view);
        Vector2 max = ImpGizmo.ViewToWorld(view, camera_2d, view);
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
            Vector2 a = ImpGizmo.WorldToView(new Vector2(x, min.Y), camera_2d, view);
            Vector2 b = ImpGizmo.WorldToView(new Vector2(x, max.Y), camera_2d, view);
            Color col = MathF.Abs(x) < 0.01f ? axis
                : (MathF.Abs(x / major_every - MathF.Round(x / major_every)) < 0.01f ? major : minor);
            Raylib.DrawLineEx(a, b, 1f, col);
        }
        for (float y = y0; y <= y1; y += step)
        {
            Vector2 a = ImpGizmo.WorldToView(new Vector2(min.X, y), camera_2d, view);
            Vector2 b = ImpGizmo.WorldToView(new Vector2(max.X, y), camera_2d, view);
            Color col = MathF.Abs(y) < 0.01f ? axis
                : (MathF.Abs(y / major_every - MathF.Round(y / major_every)) < 0.01f ? major : minor);
            Raylib.DrawLineEx(a, b, 1f, col);
        }
    }

    ImpAsset _drop_asset;
    ImpComp _drop_comp;

    public override void Cursor_OnHover(ImpPlayer player, double dt)
    {
        base.Cursor_OnHover(player, dt);
        Drop_Tick(player, (float)dt);
    }

    public override void CursorGrab_HoveredAsTarget(ImpPlayer player, ImpComp dropped, bool hovered)
    {
        base.CursorGrab_HoveredAsTarget(player, dropped, hovered);
        Drop_Bind(hovered ? Drop_AssetOf(dropped) : null, player);
    }

    public override void CursorGrab_DroppedOn(ImpPlayer player, ImpComp dropped)
    {
        base.CursorGrab_DroppedOn(player, dropped);
        if (_drop_asset == null) _drop_asset = Drop_AssetOf(dropped);
        if (_drop_asset == null) return;
        _drop_asset.SceneDrop_DropOnComp(Drop_Pick(player) ?? scene?.root, player);
        Drop_Bind(null, player);
    }

    public bool Drop_World3(ImpPlayer player, out Vector3 pos, out ImpComp hit)
    {
        pos = default;
        hit = null;
        if (scene?.root == null) return false;
        Ray ray = ImpGizmo.ScreenToRay3(player.cursor.position, camera, Dimensions_Get());
        ImpComp3D c = Imp3D.Pick_Comp3D(scene.root, ray, out pos);
        if (c != null) { hit = c; return true; }
        return Imp3D.Ray_Plane(ray, Vector3.Zero, Vector3.UnitY, out pos);
    }

    public Vector2 Drop_World2(ImpPlayer player)
    {
        return ImpGizmo.ScreenToWorld(player.cursor.position, camera_2d, Dimensions_Get());
    }

    public ImpComp Drop_Pick(ImpPlayer player)
    {
        if (scene?.root == null) return null;
        if (edit_mode == ESceneEditorMode.Mode_3D)
        {
            Drop_World3(player, out _, out ImpComp hit);
            return hit;
        }
        return C2_Gizmo.Pick(scene.root, camera_2d, Dimensions_Get(), player.cursor.position);
    }

    static ImpAsset Drop_AssetOf(ImpComp dropped)
    {
        object payload = dropped?.CursorGrab_Payload();
        if (payload is ImpAsset asset) return asset;
        if (payload is string path) return ImpAsset.Load(path);
        return null;
    }

    void Drop_Bind(ImpAsset asset, ImpPlayer player)
    {
        if (_drop_asset == asset) return;
        if (_drop_asset != null)
        {
            if (_drop_comp != null)
            {
                _drop_asset.SceneDrop_CompExit(_drop_comp, player);
                _drop_comp = null;
            }
            _drop_asset.SceneDrop_Exit(this, player);
        }
        _drop_asset = asset;
        if (_drop_asset != null) _drop_asset.SceneDrop_Enter(this, player);
    }

    void Drop_Tick(ImpPlayer player, float dt)
    {
        if (_drop_asset == null || !player.grab_is_active) return;
        ImpComp hit = Drop_Pick(player);
        if (hit != _drop_comp)
        {
            if (_drop_comp != null) _drop_asset.SceneDrop_CompExit(_drop_comp, player);
            _drop_comp = hit;
            if (_drop_comp != null) _drop_asset.SceneDrop_CompEnter(_drop_comp, player);
        }
        _drop_asset.SceneDrop_Update(this, dt, player);
    }
}
