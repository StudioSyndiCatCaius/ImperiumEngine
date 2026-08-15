using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._3D;

[ImpClass(Hidden = true)]
public class C3_Gizmo : Imp3D
{
    public TGizmoData gizmo_data;
    public bool is_dragging;
    public EGizmoHandle hover_handle = EGizmoHandle.None;
    public EGizmoHandle drag_handle = EGizmoHandle.None;

    Vector3 _origin0;
    Vector3 _ax, _ay, _az;
    Vector3 _grab;
    Vector2 _grab_screen;
    float _grab_angle;
    float _axis_len;

    readonly List<Imp3D> _sel = new();
    readonly List<TTransform3> _start = new();

    // Local transforms as the drag began. _start is world space and the live selection can
    // change under us, so undo keeps its own snapshot of what it has to put back.
    readonly List<Imp3D> _undo_sel = new();
    readonly List<TTransform3> _undo_start = new();

    public bool IsBusy => is_dragging || hover_handle != EGizmoHandle.None;

    public bool Interact(Camera3D cam, TDimensions2 vp, ImpPlayer player, ImpComp hog, bool allow_new)
    {
        if (gizmo_data == null) return false;
        gizmo_data.snap_active = ImpPlayer.Key_IsHeld(EInputKey.Key_LeftControl)
            || ImpPlayer.Key_IsHeld(EInputKey.Key_RightControl);

        Collect();
        if (_sel.Count == 0)
        {
            hover_handle = EGizmoHandle.None;
            if (is_dragging) EndDrag(player, hog);
            return false;
        }

        Vector3 origin = Origin();
        Axes(_sel[0], out Vector3 ax, out Vector3 ay, out Vector3 az);
        float axis_len = ScreenWorldSize(cam, origin, vp.size.Y) * ImpGizmo.AxisPx;
        Vector2 mouse = player.cursor.position;
        Ray ray = ImpGizmo.ScreenToRay3(mouse, cam, vp);

        if (is_dragging)
        {
            ApplyDrag(ray, mouse, cam, vp);
            if (!ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left))
                EndDrag(player, hog);
            return true;
        }

        hover_handle = allow_new ? HitTest(cam, vp, mouse, origin, ax, ay, az, axis_len) : EGizmoHandle.None;
        if (allow_new && hover_handle != EGizmoHandle.None && ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left))
        {
            BeginDrag(ray, mouse, origin, ax, ay, az, axis_len);
            if (player.input_hog == null) player.input_hog = hog;
            return true;
        }
        return hover_handle != EGizmoHandle.None;
    }

    Camera3D _draw_cam;
    TDimensions2 _draw_vp;

    public void Draw(Camera3D cam, TDimensions2 vp, ImpComp occ_root = null) // occ_root kept for call sites; outlines no longer raycast the scene
    {
        if (gizmo_data == null) return;
        Collect();
        if (_sel.Count == 0) return;
        _draw_cam = cam;
        _draw_vp = vp;

        bool freeze = is_dragging && gizmo_data.mode != EGizmoMode.Translate;
        Vector3 origin = freeze ? _origin0 : Origin();
        Vector3 ax, ay, az;
        if (freeze) { ax = _ax; ay = _ay; az = _az; }
        else Axes(_sel[0], out ax, out ay, out az);
        float axis_len = freeze ? _axis_len : ScreenWorldSize(cam, origin, vp.size.Y) * ImpGizmo.AxisPx;
        if (axis_len < 1e-5f) return;
        if (!ImpGizmo.PointInFront(origin, cam)) return;

        EGizmoHandle hot = is_dragging ? drag_handle : hover_handle;
        EGizmoMode mode = gizmo_data.mode;

        if (mode == EGizmoMode.Translate)
        {
            DrawAxis(cam, vp, origin, ax, axis_len, EGizmoHandle.AxisX, hot);
            DrawAxis(cam, vp, origin, ay, axis_len, EGizmoHandle.AxisY, hot);
            DrawAxis(cam, vp, origin, az, axis_len, EGizmoHandle.AxisZ, hot);
            DrawPlane(cam, vp, origin, ax, ay, az, axis_len, EGizmoHandle.PlaneXY, hot);
            DrawPlane(cam, vp, origin, ax, az, ay, axis_len, EGizmoHandle.PlaneXZ, hot);
            DrawPlane(cam, vp, origin, ay, az, ax, axis_len, EGizmoHandle.PlaneYZ, hot);
        }
        else if (mode == EGizmoMode.Rotate)
        {
            DrawRing(cam, vp, origin, ay, az, axis_len, EGizmoHandle.AxisX, hot);
            DrawRing(cam, vp, origin, ax, az, axis_len, EGizmoHandle.AxisY, hot);
            DrawRing(cam, vp, origin, ax, ay, axis_len, EGizmoHandle.AxisZ, hot);
        }
        else
        {
            DrawScale(cam, vp, origin, ax, axis_len, EGizmoHandle.AxisX, hot);
            DrawScale(cam, vp, origin, ay, axis_len, EGizmoHandle.AxisY, hot);
            DrawScale(cam, vp, origin, az, axis_len, EGizmoHandle.AxisZ, hot);
            Vector2 o = ImpGizmo.WorldToScreen3(origin, cam, vp);
            ImpGizmo.DrawBox2(o, ImpGizmo.ScaleBox + 1.5f, ImpGizmo.AxisColor(EGizmoHandle.Uniform, hot, drag_handle));
        }

        DrawSelection(cam, vp);
    }

    // Everything whose projected bounds overlap the marquee rect.
    public static void PickRect(ImpComp root, Camera3D cam, TDimensions2 vp, Vector2 min, Vector2 max, List<ImpComp> found)
    {
        Vector3[] corners = new Vector3[8];
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible) return;
            if (n is Imp3D c3 && n is not C3_Gizmo && Pickable(c3))
            {
                Comp3D_WorldCorners(c3, corners);
                Vector2 lo = new(float.MaxValue);
                Vector2 hi = new(float.MinValue);
                bool any = false;
                for (int i = 0; i < 8; i++)
                {
                    if (!ImpGizmo.PointInFront(corners[i], cam)) continue;
                    Vector2 s = ImpGizmo.WorldToScreen3(corners[i], cam, vp);
                    lo = Vector2.Min(lo, s);
                    hi = Vector2.Max(hi, s);
                    any = true;
                }
                if (any && hi.X >= min.X && lo.X <= max.X && hi.Y >= min.Y && lo.Y <= max.Y) found.Add(c3);
            }
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        Walk(root);
    }

    void Collect()
    {
        _sel.Clear();
        if (gizmo_data == null) return;
        for (int i = 0; i < gizmo_data.selected_comps.Count; i++)
        {
            if (gizmo_data.selected_comps[i] is not Imp3D c || c is C3_Gizmo) continue;
            if (AncestorSelected(c)) continue;
            _sel.Add(c);
        }
    }

    bool AncestorSelected(ImpComp c)
    {
        for (ImpComp p = c.parent; p != null; p = p.parent)
            if (gizmo_data.Selection_Contains(p)) return true;
        return false;
    }

    Vector3 Origin()
    {
        Vector3 p = Vector3.Zero;
        for (int i = 0; i < _sel.Count; i++) p += _sel[i].Position_Get(true);
        return _sel.Count > 0 ? p / _sel.Count : Vector3.Zero;
    }

    void Axes(Imp3D first, out Vector3 ax, out Vector3 ay, out Vector3 az)
    {
        ax = Vector3.UnitX;
        ay = Vector3.UnitY;
        az = Vector3.UnitZ;
        if (gizmo_data.space != EGizmoSpace.Local || first == null) return;
        Quaternion q = ImpMath.EulerToQuat(first.Rotation_Get(true));
        ax = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, q));
        ay = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, q));
        az = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, q));
    }

    static float ScreenWorldSize(Camera3D cam, Vector3 origin, float vp_h)
    {
        float dist = Vector3.Distance(cam.Position, origin);
        float fov = cam.FovY * (MathF.PI / 180f);
        if (vp_h < 1f) vp_h = 1f;
        return (2f * dist * MathF.Tan(fov * 0.5f)) / vp_h;
    }

    EGizmoHandle HitTest(Camera3D cam, TDimensions2 vp, Vector2 mouse, Vector3 origin,
        Vector3 ax, Vector3 ay, Vector3 az, float len)
    {
        EGizmoHandle best = EGizmoHandle.None;
        float best_d = ImpGizmo.HitPx;
        EGizmoMode mode = gizmo_data.mode;

        void Consider(EGizmoHandle h, float d)
        {
            if (d < best_d)
            {
                best_d = d;
                best = h;
            }
        }

        Vector2 o = ImpGizmo.WorldToScreen3(origin, cam, vp);

        if (mode == EGizmoMode.Translate)
        {
            ConsiderPlane(EGizmoHandle.PlaneXY, origin, ax, ay, len);
            ConsiderPlane(EGizmoHandle.PlaneXZ, origin, ax, az, len);
            ConsiderPlane(EGizmoHandle.PlaneYZ, origin, ay, az, len);
            ConsiderAxis(EGizmoHandle.AxisX, ax);
            ConsiderAxis(EGizmoHandle.AxisY, ay);
            ConsiderAxis(EGizmoHandle.AxisZ, az);
        }
        else if (mode == EGizmoMode.Rotate)
        {
            ConsiderRing(EGizmoHandle.AxisX, ay, az);
            ConsiderRing(EGizmoHandle.AxisY, ax, az);
            ConsiderRing(EGizmoHandle.AxisZ, ax, ay);
        }
        else
        {
            if (Vector2.Distance(mouse, o) < ImpGizmo.HitPx) Consider(EGizmoHandle.Uniform, Vector2.Distance(mouse, o));
            ConsiderTip(EGizmoHandle.AxisX, ax);
            ConsiderTip(EGizmoHandle.AxisY, ay);
            ConsiderTip(EGizmoHandle.AxisZ, az);
        }

        void ConsiderAxis(EGizmoHandle h, Vector3 axis)
        {
            Vector3 end = origin + axis * len;
            if (!ImpGizmo.PointInFront(end, cam)) return;
            Vector2 e = ImpGizmo.WorldToScreen3(end, cam, vp);
            Consider(h, ImpGizmo.DistPointSeg(mouse, o, e));
        }

        void ConsiderTip(EGizmoHandle h, Vector3 axis)
        {
            Vector3 end = origin + axis * len;
            if (!ImpGizmo.PointInFront(end, cam)) return;
            Vector2 e = ImpGizmo.WorldToScreen3(end, cam, vp);
            Consider(h, Vector2.Distance(mouse, e));
        }

        void ConsiderPlane(EGizmoHandle h, Vector3 org, Vector3 u, Vector3 v, float alen)
        {
            float o0 = alen * 0.28f;
            float s = alen * 0.22f;
            Vector3 a = org + u * o0 + v * o0;
            Vector3 b = a + u * s;
            Vector3 c = a + u * s + v * s;
            Vector3 d = a + v * s;
            if (!ImpGizmo.PointInFront(a, cam)) return;
            Vector2 sa = ImpGizmo.WorldToScreen3(a, cam, vp);
            Vector2 sb = ImpGizmo.WorldToScreen3(b, cam, vp);
            Vector2 sc = ImpGizmo.WorldToScreen3(c, cam, vp);
            Vector2 sd = ImpGizmo.WorldToScreen3(d, cam, vp);
            if (ImpGizmo.PointInQuad(mouse, sa, sb, sc, sd)) Consider(h, 0f);
        }

        void ConsiderRing(EGizmoHandle h, Vector3 u, Vector3 v)
        {
            const int segs = 48;
            Vector3 to_cam = cam.Position - origin;
            Vector2 prev = default;
            bool have = false;
            for (int i = 0; i <= segs; i++)
            {
                float a = i * (MathF.PI * 2f) / segs;
                Vector3 p = origin + (u * MathF.Cos(a) + v * MathF.Sin(a)) * len;
                // Only the camera-facing half is drawn, so only that half is grabbable.
                if (!ImpGizmo.PointInFront(p, cam) || Vector3.Dot(p - origin, to_cam) < 0f)
                {
                    have = false;
                    continue;
                }
                Vector2 s = ImpGizmo.WorldToScreen3(p, cam, vp);
                if (have) Consider(h, ImpGizmo.DistPointSeg(mouse, prev, s));
                prev = s;
                have = true;
            }
        }

        return best;
    }

    void BeginDrag(Ray ray, Vector2 mouse, Vector3 origin, Vector3 ax, Vector3 ay, Vector3 az, float axis_len)
    {
        is_dragging = true;
        drag_handle = hover_handle;
        _origin0 = origin;
        _ax = ax; _ay = ay; _az = az;
        _axis_len = axis_len;
        _grab_screen = mouse;
        _start.Clear();
        _undo_sel.Clear();
        _undo_start.Clear();
        for (int i = 0; i < _sel.Count; i++)
        {
            _start.Add(_sel[i].Transform_Get(true));
            _undo_sel.Add(_sel[i]);
            _undo_start.Add(_sel[i].transform);
        }

        Vector3 axis = HandleAxis(drag_handle, ax, ay, az);
        Vector3 plane_n = DragPlaneNormal(drag_handle, ax, ay, az, ray.Direction, gizmo_data.mode);
        if (Ray_Plane(ray, origin, plane_n, out Vector3 hit))
            _grab = hit;
        else
            _grab = origin;

        if (gizmo_data.mode == EGizmoMode.Rotate)
        {
            Vector3 v = _grab - origin;
            Vector3 u = Vector3.Normalize(Perp(axis));
            Vector3 w = Vector3.Normalize(Vector3.Cross(axis, u));
            _grab_angle = MathF.Atan2(Vector3.Dot(v, w), Vector3.Dot(v, u));
        }
    }

    void ApplyDrag(Ray ray, Vector2 mouse, Camera3D cam, TDimensions2 vp)
    {
        Vector3 axis = HandleAxis(drag_handle, _ax, _ay, _az);
        Vector3 plane_n = DragPlaneNormal(drag_handle, _ax, _ay, _az, ray.Direction, gizmo_data.mode);
        Ray_Plane(ray, _origin0, plane_n, out Vector3 hit);

        if (gizmo_data.mode == EGizmoMode.Translate)
            ApplyTranslate(hit);
        else if (gizmo_data.mode == EGizmoMode.Rotate)
            ApplyRotate(hit, axis);
        else
            ApplyScale(hit, mouse, cam, vp);
    }

    void ApplyTranslate(Vector3 hit)
    {
        Vector3 delta = hit - _grab;
        if (drag_handle is EGizmoHandle.AxisX or EGizmoHandle.AxisY or EGizmoHandle.AxisZ)
        {
            Vector3 axis = HandleAxis(drag_handle, _ax, _ay, _az);
            delta = axis * Vector3.Dot(delta, axis);
        }
        else if (drag_handle == EGizmoHandle.PlaneXY) delta -= _az * Vector3.Dot(delta, _az);
        else if (drag_handle == EGizmoHandle.PlaneXZ) delta -= _ay * Vector3.Dot(delta, _ay);
        else if (drag_handle == EGizmoHandle.PlaneYZ) delta -= _ax * Vector3.Dot(delta, _ax);

        float step = gizmo_data.snap_translate;
        for (int i = 0; i < _sel.Count; i++)
        {
            Vector3 pos = _start[i].position + delta;
            if (gizmo_data.snap_active && step > 1e-8f)
            {
                bool lock_x = drag_handle is EGizmoHandle.AxisY or EGizmoHandle.AxisZ or EGizmoHandle.PlaneYZ;
                bool lock_y = drag_handle is EGizmoHandle.AxisX or EGizmoHandle.AxisZ or EGizmoHandle.PlaneXZ;
                bool lock_z = drag_handle is EGizmoHandle.AxisX or EGizmoHandle.AxisY or EGizmoHandle.PlaneXY;
                if (!lock_x) pos.X = ImpGizmo.Snap(pos.X, step);
                if (!lock_y) pos.Y = ImpGizmo.Snap(pos.Y, step);
                if (!lock_z) pos.Z = ImpGizmo.Snap(pos.Z, step);
            }
            _sel[i].Position_Set(pos, true);
        }
    }

    void ApplyRotate(Vector3 hit, Vector3 axis)
    {
        if (axis.LengthSquared() < 1e-8f) return;
        axis = Vector3.Normalize(axis);
        Vector3 u = Vector3.Normalize(Perp(axis));
        Vector3 w = Vector3.Normalize(Vector3.Cross(axis, u));
        Vector3 v = hit - _origin0;
        float ang = MathF.Atan2(Vector3.Dot(v, w), Vector3.Dot(v, u)) - _grab_angle;
        float deg = ang * (180f / MathF.PI);
        if (gizmo_data.snap_active) deg = ImpGizmo.Snap(deg, gizmo_data.snap_rotate_deg);
        Quaternion dq = Quaternion.CreateFromAxisAngle(axis, deg * (MathF.PI / 180f));

        for (int i = 0; i < _sel.Count; i++)
        {
            Vector3 p = _origin0 + Vector3.Transform(_start[i].position - _origin0, dq);
            Quaternion r = dq * ImpMath.EulerToQuat(_start[i].rotation);
            _sel[i].Position_Set(p, true);
            _sel[i].Rotation_Set(ImpMath.QuatToEuler(r), true);
        }
    }

    void ApplyScale(Vector3 hit, Vector2 mouse, Camera3D cam, TDimensions2 vp)
    {
        float ratio = 1f;
        if (drag_handle == EGizmoHandle.Uniform)
        {
            Vector2 o = ImpGizmo.WorldToScreen3(_origin0, cam, vp);
            float d0 = Vector2.Distance(_grab_screen, o);
            float d1 = Vector2.Distance(mouse, o);
            ratio = d0 > 1f ? d1 / d0 : 1f;
        }
        else
        {
            Vector3 axis = HandleAxis(drag_handle, _ax, _ay, _az);
            float t0 = Vector3.Dot(_grab - _origin0, axis);
            float t1 = Vector3.Dot(hit - _origin0, axis);
            ratio = MathF.Abs(t0) > 1e-5f ? t1 / t0 : 1f;
        }
        ratio = Math.Clamp(ratio, 0.01f, 100f);
        if (gizmo_data.snap_active)
            ratio = MathF.Max(0.01f, ImpGizmo.Snap(ratio, gizmo_data.snap_scale));

        Vector3 mul = Vector3.One;
        if (drag_handle == EGizmoHandle.AxisX) mul = new Vector3(ratio, 1f, 1f);
        else if (drag_handle == EGizmoHandle.AxisY) mul = new Vector3(1f, ratio, 1f);
        else if (drag_handle == EGizmoHandle.AxisZ) mul = new Vector3(1f, 1f, ratio);
        else mul = new Vector3(ratio);

        for (int i = 0; i < _sel.Count; i++)
        {
            Vector3 lp = _start[i].position - _origin0;
            Vector3 local = new(Vector3.Dot(lp, _ax), Vector3.Dot(lp, _ay), Vector3.Dot(lp, _az));
            local *= mul;
            _sel[i].Position_Set(_origin0 + _ax * local.X + _ay * local.Y + _az * local.Z, true);
            Vector3 s = _start[i].scale * mul;
            s = Vector3.Max(s, new Vector3(0.01f));
            _sel[i].Scale_Set(s, true);
        }
    }

    void EndDrag(ImpPlayer player, ImpComp hog)
    {
        // The whole gesture is one undo step, not one per frame the mouse moved.
        string label = gizmo_data.mode switch
        {
            EGizmoMode.Rotate => "Rotate",
            EGizmoMode.Scale => "Scale",
            _ => "Move",
        };
        ImpUndo.Group_Begin(label);
        for (int i = 0; i < _undo_sel.Count && i < _undo_start.Count; i++)
            ImpUndo.Comp_Transformed(_undo_sel[i], _undo_start[i], label);
        ImpUndo.Group_End();
        _undo_sel.Clear();
        _undo_start.Clear();

        is_dragging = false;
        drag_handle = EGizmoHandle.None;
        if (player != null && player.input_hog == hog) player.input_hog = null;
    }

    static Vector3 HandleAxis(EGizmoHandle h, Vector3 ax, Vector3 ay, Vector3 az) => h switch
    {
        EGizmoHandle.AxisX or EGizmoHandle.PlaneYZ => ax,
        EGizmoHandle.AxisY or EGizmoHandle.PlaneXZ => ay,
        EGizmoHandle.AxisZ or EGizmoHandle.PlaneXY => az,
        _ => Vector3.UnitY,
    };

    static Vector3 DragPlaneNormal(EGizmoHandle h, Vector3 ax, Vector3 ay, Vector3 az, Vector3 cam_dir, EGizmoMode mode)
    {
        if (h == EGizmoHandle.PlaneXY) return az;
        if (h == EGizmoHandle.PlaneXZ) return ay;
        if (h == EGizmoHandle.PlaneYZ) return ax;
        Vector3 axis = HandleAxis(h, ax, ay, az);
        // A rotate drag sweeps around the ring, so its plane is the one the ring lies in.
        // Translate/scale slide along the axis, so their plane has to contain it instead.
        if (mode == EGizmoMode.Rotate) return axis;
        Vector3 n = Vector3.Cross(axis, Vector3.Cross(cam_dir, axis));
        if (n.LengthSquared() < 1e-8f) n = Perp(axis);
        return Vector3.Normalize(n);
    }

    static Vector3 Perp(Vector3 a)
    {
        Vector3 p = Vector3.Cross(a, Vector3.UnitY);
        if (p.LengthSquared() < 1e-6f) p = Vector3.Cross(a, Vector3.UnitX);
        return p;
    }

    void DrawWorldLine(Vector3 a, Vector3 b, Color col, float thick)
    {
        if (!ImpGizmo.PointInFront(a, _draw_cam) || !ImpGizmo.PointInFront(b, _draw_cam)) return;
        Raylib.DrawLineEx(
            ImpGizmo.WorldToScreen3(a, _draw_cam, _draw_vp),
            ImpGizmo.WorldToScreen3(b, _draw_cam, _draw_vp),
            thick, col);
    }

    void DrawAxis(Camera3D cam, TDimensions2 vp, Vector3 origin, Vector3 axis, float len,
        EGizmoHandle handle, EGizmoHandle hot)
    {
        Vector3 end = origin + axis * len;
        if (!ImpGizmo.PointInFront(origin, cam) || !ImpGizmo.PointInFront(end, cam)) return;
        Color col = ImpGizmo.AxisColor(handle, hot, drag_handle);
        DrawWorldLine(origin, end, col, ImpGizmo.LineThick);
        Vector2 tip = ImpGizmo.WorldToScreen3(end, cam, vp);
        Vector2 from = ImpGizmo.WorldToScreen3(origin + axis * len * 0.78f, cam, vp);
        ImpGizmo.DrawArrow2(from, tip, col, ImpGizmo.LineThick);
    }

    void DrawScale(Camera3D cam, TDimensions2 vp, Vector3 origin, Vector3 axis, float len,
        EGizmoHandle handle, EGizmoHandle hot)
    {
        Vector3 end = origin + axis * len;
        if (!ImpGizmo.PointInFront(end, cam)) return;
        Color col = ImpGizmo.AxisColor(handle, hot, drag_handle);
        DrawWorldLine(origin, end, col, ImpGizmo.LineThick);
        ImpGizmo.DrawBox2(ImpGizmo.WorldToScreen3(end, cam, vp), ImpGizmo.ScaleBox, col);
    }

    void DrawPlane(Camera3D cam, TDimensions2 vp, Vector3 origin, Vector3 u, Vector3 v, Vector3 unused,
        float len, EGizmoHandle handle, EGizmoHandle hot)
    {
        float o0 = len * 0.28f;
        float s = len * 0.22f;
        Vector3 a = origin + u * o0 + v * o0;
        Vector3 b = a + u * s;
        Vector3 c = a + u * s + v * s;
        Vector3 d = a + v * s;
        if (!ImpGizmo.PointInFront(a, cam)) return;
        Color col = ImpGizmo.AxisColor(handle, hot, drag_handle);
        ImpGizmo.DrawQuad(
            ImpGizmo.WorldToScreen3(a, cam, vp),
            ImpGizmo.WorldToScreen3(b, cam, vp),
            ImpGizmo.WorldToScreen3(c, cam, vp),
            ImpGizmo.WorldToScreen3(d, cam, vp),
            ImpGizmo.WithAlpha(col, 70), col);
    }

    void DrawRing(Camera3D cam, TDimensions2 vp, Vector3 origin, Vector3 u, Vector3 v, float radius,
        EGizmoHandle handle, EGizmoHandle hot)
    {
        Color col = ImpGizmo.AxisColor(handle, hot, drag_handle);
        const int segs = 48;
        Vector3 to_cam = cam.Position - origin;
        Vector3 prev_w = default;
        bool have = false;
        for (int i = 0; i <= segs; i++)
        {
            float a = i * (MathF.PI * 2f) / segs;
            Vector3 p = origin + (u * MathF.Cos(a) + v * MathF.Sin(a)) * radius;
            float facing = Vector3.Dot(p - origin, to_cam);
            if (!ImpGizmo.PointInFront(p, cam) || facing < 0f) { have = false; continue; }
            if (have)
            {
                Raylib.DrawLineEx(
                    ImpGizmo.WorldToScreen3(prev_w, cam, vp),
                    ImpGizmo.WorldToScreen3(p, cam, vp),
                    handle == hot ? 6.5f : ImpGizmo.LineThick,
                    col);
            }
            prev_w = p;
            have = true;
        }
    }

    void DrawSelection(Camera3D cam, TDimensions2 vp)
    {
        Vector3[] corners = new Vector3[8];
        int[] e = { 0, 1, 1, 3, 3, 2, 2, 0, 4, 5, 5, 7, 7, 6, 6, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
        for (int i = 0; i < _sel.Count; i++)
        {
            Comp3D_WorldCorners(_sel[i], corners);
            for (int k = 0; k < e.Length; k += 2)
            {
                Vector3 wa = corners[e[k]], wb = corners[e[k + 1]];
                if (!ImpGizmo.PointInFront(wa, cam) || !ImpGizmo.PointInFront(wb, cam)) continue;
                Raylib.DrawLineEx(
                    ImpGizmo.WorldToScreen3(wa, cam, vp),
                    ImpGizmo.WorldToScreen3(wb, cam, vp),
                    2.2f, ImpGizmo.ColSelect);
            }
        }
    }
}
