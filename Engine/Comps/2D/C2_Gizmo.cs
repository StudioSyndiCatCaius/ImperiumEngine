using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Comps._2D;

public class C2_Gizmo : Imp2D
{
    public TGizmoData gizmo_data;
    public bool is_dragging;
    public EGizmoHandle hover_handle = EGizmoHandle.None;
    public EGizmoHandle drag_handle = EGizmoHandle.None;

    Vector2 _origin0;
    Vector2 _ax, _ay;
    Vector2 _grab;
    Vector2 _grab_screen;
    float _grab_angle;
    float _axis_len;

    readonly List<Imp2D> _sel = new();
    readonly List<TTransform2> _start = new();
    readonly List<Vector2> _pivot_start = new();

    // Local transform and raw pivot as the drag began. _start is world space and _pivot_start is
    // resolved to pixels, so undo keeps its own snapshot of the fields it has to put back.
    readonly List<Imp2D> _undo_sel = new();
    readonly List<TTransform2> _undo_start = new();
    readonly List<Vector2> _undo_pivot = new();

    public bool IsBusy => is_dragging || hover_handle != EGizmoHandle.None;

    public bool Interact(TCamera2D cam, TDimensions2 vp, ImpPlayer player, ImpComp hog, bool allow_new)
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

        Vector2 origin = Origin();
        Axes(_sel[0], out Vector2 ax, out Vector2 ay);
        float z = cam.zoom <= 1e-6f ? 1f : cam.zoom;
        float axis_len = ImpGizmo.AxisPx / z;
        Vector2 mouse = player.cursor.position;
        Vector2 world = ImpGizmo.ScreenToWorld(mouse, cam, vp);

        if (is_dragging)
        {
            ApplyDrag(world, mouse, cam, vp);
            if (!ImpPlayer.Key_IsHeld(EInputKey.Mouse_Left))
                EndDrag(player, hog);
            return true;
        }

        hover_handle = allow_new ? HitTest(cam, vp, mouse, origin, ax, ay, axis_len) : EGizmoHandle.None;
        if (allow_new && hover_handle != EGizmoHandle.None && ImpPlayer.Key_IsPressed(EInputKey.Mouse_Left))
        {
            BeginDrag(world, mouse, origin, ax, ay, axis_len);
            if (player.input_hog == null) player.input_hog = hog;
            return true;
        }
        return hover_handle != EGizmoHandle.None;
    }

    public void Draw(TCamera2D cam, TDimensions2 vp)
    {
        if (gizmo_data == null) return;
        Collect();
        if (_sel.Count == 0) return;

        // A pivot drag moves the origin, so the gizmo has to track it rather than freeze.
        bool freeze = is_dragging && drag_handle != EGizmoHandle.Pivot
            && gizmo_data.mode != EGizmoMode.Translate;
        Vector2 origin = freeze ? _origin0 : Origin();
        Vector2 ax, ay;
        if (freeze) { ax = _ax; ay = _ay; }
        else Axes(_sel[0], out ax, out ay);
        float z = cam.zoom <= 1e-6f ? 1f : cam.zoom;
        float axis_len = freeze ? _axis_len : ImpGizmo.AxisPx / z;
        EGizmoHandle hot = is_dragging ? drag_handle : hover_handle;
        EGizmoMode mode = gizmo_data.mode;

        Vector2 o = ImpGizmo.WorldToScreen(origin, cam, vp);
        Vector2 px = ImpGizmo.WorldToScreen(origin + ax * axis_len, cam, vp);
        Vector2 py = ImpGizmo.WorldToScreen(origin + ay * axis_len, cam, vp);

        if (mode == EGizmoMode.Translate)
        {
            ImpGizmo.DrawArrow2(o, px, ImpGizmo.AxisColor(EGizmoHandle.AxisX, hot, drag_handle), ImpGizmo.LineThick);
            ImpGizmo.DrawArrow2(o, py, ImpGizmo.AxisColor(EGizmoHandle.AxisY, hot, drag_handle), ImpGizmo.LineThick);
            DrawPlane(cam, vp, origin, ax, ay, axis_len, hot);
        }
        else if (mode == EGizmoMode.Rotate)
        {
            Color col = ImpGizmo.AxisColor(EGizmoHandle.AxisZ, hot, drag_handle);
            ImpGizmo.DrawRing2(o, Vector2.Distance(o, px), col, ImpGizmo.LineThick);
        }
        else
        {
            Raylib.DrawLineEx(o, px, ImpGizmo.LineThick, ImpGizmo.AxisColor(EGizmoHandle.AxisX, hot, drag_handle));
            Raylib.DrawLineEx(o, py, ImpGizmo.LineThick, ImpGizmo.AxisColor(EGizmoHandle.AxisY, hot, drag_handle));
            ImpGizmo.DrawBox2(px, ImpGizmo.ScaleBox, ImpGizmo.AxisColor(EGizmoHandle.AxisX, hot, drag_handle));
            ImpGizmo.DrawBox2(py, ImpGizmo.ScaleBox, ImpGizmo.AxisColor(EGizmoHandle.AxisY, hot, drag_handle));
            ImpGizmo.DrawBox2(o, ImpGizmo.ScaleBox + 1.5f, ImpGizmo.AxisColor(EGizmoHandle.Uniform, hot, drag_handle));
        }

        Color pivot_col = ImpGizmo.AxisColor(EGizmoHandle.Pivot, hot, drag_handle);
        ImpGizmo.DrawRing2(o, ImpGizmo.PivotPx, pivot_col, 2f, 20);
        Raylib.DrawCircleV(o, 2.5f, pivot_col);

        DrawSelection(cam, vp);
    }

    public static Imp2D Pick(ImpComp root, TCamera2D cam, TDimensions2 vp, Vector2 screen)
    {
        Imp2D best = null;
        float best_area = float.MaxValue;
        Vector2 world = ImpGizmo.ScreenToWorld(screen, cam, vp);
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible) return;
            if (n is Imp2D c2 && n is not C2_Gizmo && !c2.IsGroupPivot)
            {
                if (HitComp(c2, world))
                {
                    Vector2 sz = c2.Size_Layout();
                    float area = MathF.Max(1f, sz.X * sz.Y);
                    if (area < best_area)
                    {
                        best_area = area;
                        best = c2;
                    }
                }
            }
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        Walk(root);
        return best;
    }

    // Everything whose screen bounds overlap the marquee rect.
    public static void PickRect(ImpComp root, TCamera2D cam, TDimensions2 vp, Vector2 min, Vector2 max, List<ImpComp> found)
    {
        Vector2[] corners = new Vector2[4];
        void Walk(ImpComp n)
        {
            if (n == null || !n.is_visible) return;
            if (n is Imp2D c2 && n is not C2_Gizmo && !c2.IsGroupPivot)
            {
                CompCorners(c2, corners);
                Vector2 lo = new(float.MaxValue);
                Vector2 hi = new(float.MinValue);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 s = ImpGizmo.WorldToScreen(corners[i], cam, vp);
                    lo = Vector2.Min(lo, s);
                    hi = Vector2.Max(hi, s);
                }
                if (hi.X >= min.X && lo.X <= max.X && hi.Y >= min.Y && lo.Y <= max.Y) found.Add(c2);
            }
            for (int i = 0; i < n.children.Count; i++) Walk(n.children[i]);
        }
        Walk(root);
    }

    public static Vector2 PivotLocal(Imp2D c)
    {
        return c == null ? Vector2.Zero : c.Pivot_Local();
    }

    // The transform position IS the pivot point.
    public static Vector2 PivotWorld(Imp2D c)
    {
        return c == null ? Vector2.Zero : c.Position_Get(true);
    }

    public static void CompCorners(Imp2D c, Vector2[] corners)
    {
        Vector2 pos = c.Position_Get(true);
        float rot = c.Rotation_Get(true);
        Vector2 sc = c.Scale_Get(true);
        Vector2 pl = PivotLocal(c);
        Vector2 sz = c.Size_Layout();
        // pos is already the pivot point in world space.
        Vector2 pivot_w = pos;
        Vector2[] local =
        {
            new(0, 0), new(sz.X, 0), new(sz.X, sz.Y), new(0, sz.Y),
        };
        for (int i = 0; i < 4; i++)
        {
            ImpGizmo.Rotate2((local[i] - pl) * sc, rot, out Vector2 r);
            corners[i] = pivot_w + r;
        }
    }

    static bool HitComp(Imp2D c, Vector2 world)
    {
        Vector2[] corners = new Vector2[4];
        CompCorners(c, corners);
        return ImpGizmo.PointInQuad(world, corners[0], corners[1], corners[2], corners[3]);
    }

    void Collect()
    {
        _sel.Clear();
        if (gizmo_data == null) return;
        for (int i = 0; i < gizmo_data.selected_comps.Count; i++)
        {
            if (gizmo_data.selected_comps[i] is not Imp2D c || c is C2_Gizmo) continue;
            bool skip = false;
            for (ImpComp p = c.parent; p != null; p = p.parent)
            {
                if (gizmo_data.Selection_Contains(p)) { skip = true; break; }
            }
            if (!skip) _sel.Add(c);
        }
    }

    Vector2 Origin()
    {
        Vector2 p = Vector2.Zero;
        for (int i = 0; i < _sel.Count; i++) p += PivotWorld(_sel[i]);
        return _sel.Count > 0 ? p / _sel.Count : Vector2.Zero;
    }

    void Axes(Imp2D first, out Vector2 ax, out Vector2 ay)
    {
        ax = Vector2.UnitX;
        ay = Vector2.UnitY;
        if (gizmo_data.space != EGizmoSpace.Local || first == null) return;
        ImpGizmo.Rotate2(Vector2.UnitX, first.Rotation_Get(true), out ax);
        ImpGizmo.Rotate2(Vector2.UnitY, first.Rotation_Get(true), out ay);
    }

    EGizmoHandle HitTest(TCamera2D cam, TDimensions2 vp, Vector2 mouse, Vector2 origin,
        Vector2 ax, Vector2 ay, float len)
    {
        EGizmoHandle best = EGizmoHandle.None;
        float best_d = ImpGizmo.HitPx;
        void Consider(EGizmoHandle h, float d)
        {
            if (d < best_d) { best_d = d; best = h; }
        }

        Vector2 o = ImpGizmo.WorldToScreen(origin, cam, vp);
        Vector2 px = ImpGizmo.WorldToScreen(origin + ax * len, cam, vp);
        Vector2 py = ImpGizmo.WorldToScreen(origin + ay * len, cam, vp);
        EGizmoMode mode = gizmo_data.mode;

        // The pivot ring sits on the origin and wins the innermost few pixels in every mode.
        if (Vector2.Distance(mouse, o) <= ImpGizmo.PivotPx) return EGizmoHandle.Pivot;

        if (mode == EGizmoMode.Translate)
        {
            float o0 = len * 0.28f;
            float s = len * 0.22f;
            Vector2 a = ImpGizmo.WorldToScreen(origin + ax * o0 + ay * o0, cam, vp);
            Vector2 b = ImpGizmo.WorldToScreen(origin + ax * (o0 + s) + ay * o0, cam, vp);
            Vector2 c = ImpGizmo.WorldToScreen(origin + ax * (o0 + s) + ay * (o0 + s), cam, vp);
            Vector2 d = ImpGizmo.WorldToScreen(origin + ax * o0 + ay * (o0 + s), cam, vp);
            if (ImpGizmo.PointInQuad(mouse, a, b, c, d)) Consider(EGizmoHandle.PlaneXY, 0f);
            Consider(EGizmoHandle.AxisX, ImpGizmo.DistPointSeg(mouse, o, px));
            Consider(EGizmoHandle.AxisY, ImpGizmo.DistPointSeg(mouse, o, py));
        }
        else if (mode == EGizmoMode.Rotate)
        {
            float r = Vector2.Distance(o, px);
            float d = MathF.Abs(Vector2.Distance(mouse, o) - r);
            Consider(EGizmoHandle.AxisZ, d);
        }
        else
        {
            if (Vector2.Distance(mouse, o) < ImpGizmo.HitPx) Consider(EGizmoHandle.Uniform, Vector2.Distance(mouse, o));
            Consider(EGizmoHandle.AxisX, Vector2.Distance(mouse, px));
            Consider(EGizmoHandle.AxisY, Vector2.Distance(mouse, py));
        }
        return best;
    }

    void BeginDrag(Vector2 world, Vector2 mouse, Vector2 origin, Vector2 ax, Vector2 ay, float axis_len)
    {
        is_dragging = true;
        drag_handle = hover_handle;
        _origin0 = origin;
        _ax = ax; _ay = ay;
        _axis_len = axis_len;
        _grab = world;
        _grab_screen = mouse;
        _start.Clear();
        _pivot_start.Clear();
        _undo_sel.Clear();
        _undo_start.Clear();
        _undo_pivot.Clear();
        for (int i = 0; i < _sel.Count; i++)
        {
            _start.Add(_sel[i].Transform_Get(true));
            _pivot_start.Add(_sel[i].Pivot_Local());
            _undo_sel.Add(_sel[i]);
            _undo_start.Add(_sel[i].transform);
            _undo_pivot.Add(_sel[i].pivot);
        }
        Vector2 v = world - origin;
        _grab_angle = MathF.Atan2(v.Y, v.X);
    }

    void ApplyDrag(Vector2 world, Vector2 mouse, TCamera2D cam, TDimensions2 vp)
    {
        if (drag_handle == EGizmoHandle.Pivot) ApplyPivot(world);
        else if (gizmo_data.mode == EGizmoMode.Translate) ApplyTranslate(world);
        else if (gizmo_data.mode == EGizmoMode.Rotate) ApplyRotate(world);
        else ApplyScale(world, mouse, cam, vp);

        // A drag runs inside the cursor phase, after that phase's epoch bump, so the
        // cached rects from the hit test are now stale. The Position/Rotation/Scale
        // setters invalidate for themselves, but ApplyPivot writes `pivot` directly.
        // Without this the gizmo and selection outline trail the mouse by a frame.
        Imp2D.Layout_Invalidate();
    }

    // Slides the pivot through the comp while the box stays put on screen.
    void ApplyPivot(Vector2 world)
    {
        Vector2 delta = world - _grab;
        for (int i = 0; i < _sel.Count; i++)
        {
            Imp2D c = _sel[i];
            TTransform2 st = _start[i];
            ImpGizmo.Rotate2(delta, -(float)st.rotation, out Vector2 local_delta);
            Vector2 sc = st.scale;
            local_delta = new Vector2(
                MathF.Abs(sc.X) > 1e-6f ? local_delta.X / sc.X : 0f,
                MathF.Abs(sc.Y) > 1e-6f ? local_delta.Y / sc.Y : 0f);

            Vector2 pivot_local = _pivot_start[i] + local_delta;
            float step = gizmo_data.snap_translate_2d;
            if (gizmo_data.snap_active && step > 1e-8f)
            {
                pivot_local.X = ImpGizmo.Snap(pivot_local.X, step);
                pivot_local.Y = ImpGizmo.Snap(pivot_local.Y, step);
            }

            Vector2 sz = c.Size_Layout();
            c.pivot = c.normalize_pivot
                ? new Vector2(
                    MathF.Abs(sz.X) > 1e-6f ? pivot_local.X / sz.X : 0f,
                    MathF.Abs(sz.Y) > 1e-6f ? pivot_local.Y / sz.Y : 0f)
                : pivot_local;
            c.Position_Set(st.position + delta, true);
        }
    }

    void ApplyTranslate(Vector2 world)
    {
        Vector2 delta = world - _grab;
        if (drag_handle == EGizmoHandle.AxisX) delta = _ax * Vector2.Dot(delta, _ax);
        else if (drag_handle == EGizmoHandle.AxisY) delta = _ay * Vector2.Dot(delta, _ay);

        float step = gizmo_data.snap_translate_2d;
        for (int i = 0; i < _sel.Count; i++)
        {
            Vector2 pos = _start[i].position + delta;
            if (gizmo_data.snap_active && step > 1e-8f)
            {
                if (drag_handle != EGizmoHandle.AxisY) pos.X = ImpGizmo.Snap(pos.X, step);
                if (drag_handle != EGizmoHandle.AxisX) pos.Y = ImpGizmo.Snap(pos.Y, step);
            }
            _sel[i].Position_Set(pos, true);
        }
    }

    void ApplyRotate(Vector2 world)
    {
        Vector2 v = world - _origin0;
        float ang = MathF.Atan2(v.Y, v.X) - _grab_angle;
        float deg = ang * (180f / MathF.PI);
        if (gizmo_data.snap_active) deg = ImpGizmo.Snap(deg, gizmo_data.snap_rotate_deg);
        float rad = deg * (MathF.PI / 180f);
        float c = MathF.Cos(rad), s = MathF.Sin(rad);

        for (int i = 0; i < _sel.Count; i++)
        {
            Vector2 lp = _start[i].position - _origin0;
            Vector2 p = _origin0 + new Vector2(lp.X * c - lp.Y * s, lp.X * s + lp.Y * c);
            _sel[i].Position_Set(p, true);
            _sel[i].Rotation_Set((float)_start[i].rotation + deg, true);
        }
    }

    void ApplyScale(Vector2 world, Vector2 mouse, TCamera2D cam, TDimensions2 vp)
    {
        float ratio = 1f;
        if (drag_handle == EGizmoHandle.Uniform)
        {
            Vector2 o = ImpGizmo.WorldToScreen(_origin0, cam, vp);
            float d0 = Vector2.Distance(_grab_screen, o);
            float d1 = Vector2.Distance(mouse, o);
            ratio = d0 > 1f ? d1 / d0 : 1f;
        }
        else
        {
            Vector2 axis = drag_handle == EGizmoHandle.AxisY ? _ay : _ax;
            float t0 = Vector2.Dot(_grab - _origin0, axis);
            float t1 = Vector2.Dot(world - _origin0, axis);
            ratio = MathF.Abs(t0) > 1e-5f ? t1 / t0 : 1f;
        }
        ratio = Math.Clamp(ratio, 0.01f, 100f);
        if (gizmo_data.snap_active)
            ratio = MathF.Max(0.01f, ImpGizmo.Snap(ratio, gizmo_data.snap_scale));

        Vector2 mul = Vector2.One;
        if (drag_handle == EGizmoHandle.AxisX) mul = new Vector2(ratio, 1f);
        else if (drag_handle == EGizmoHandle.AxisY) mul = new Vector2(1f, ratio);
        else mul = new Vector2(ratio);

        float rot = MathF.Atan2(_ax.Y, _ax.X);
        float cr = MathF.Cos(-rot), sr = MathF.Sin(-rot);
        float cf = MathF.Cos(rot), sf = MathF.Sin(rot);

        for (int i = 0; i < _sel.Count; i++)
        {
            Vector2 lp = _start[i].position - _origin0;
            Vector2 local = new(lp.X * cr - lp.Y * sr, lp.X * sr + lp.Y * cr);
            local *= mul;
            Vector2 p = _origin0 + new Vector2(local.X * cf - local.Y * sf, local.X * sf + local.Y * cf);
            _sel[i].Position_Set(p, true);
            Vector2 s = _start[i].scale * mul;
            s = Vector2.Max(s, new Vector2(0.01f));
            _sel[i].Scale_Set(s, true);
        }
    }

    void EndDrag(ImpPlayer player, ImpComp hog)
    {
        // The whole gesture is one undo step, not one per frame the mouse moved.
        string label = drag_handle == EGizmoHandle.Pivot ? "Pivot" : gizmo_data.mode switch
        {
            EGizmoMode.Rotate => "Rotate",
            EGizmoMode.Scale => "Scale",
            _ => "Move",
        };
        ImpUndo.Group_Begin(label);
        for (int i = 0; i < _undo_sel.Count && i < _undo_start.Count && i < _undo_pivot.Count; i++)
            ImpUndo.Comp_Transformed(_undo_sel[i], _undo_start[i], _undo_pivot[i], label);
        ImpUndo.Group_End();
        _undo_sel.Clear();
        _undo_start.Clear();
        _undo_pivot.Clear();

        is_dragging = false;
        drag_handle = EGizmoHandle.None;
        if (player != null && player.input_hog == hog) player.input_hog = null;
    }

    void DrawPlane(TCamera2D cam, TDimensions2 vp, Vector2 origin, Vector2 ax, Vector2 ay, float len, EGizmoHandle hot)
    {
        float o0 = len * 0.28f;
        float s = len * 0.22f;
        Color col = ImpGizmo.AxisColor(EGizmoHandle.PlaneXY, hot, drag_handle);
        ImpGizmo.DrawQuad(
            ImpGizmo.WorldToScreen(origin + ax * o0 + ay * o0, cam, vp),
            ImpGizmo.WorldToScreen(origin + ax * (o0 + s) + ay * o0, cam, vp),
            ImpGizmo.WorldToScreen(origin + ax * (o0 + s) + ay * (o0 + s), cam, vp),
            ImpGizmo.WorldToScreen(origin + ax * o0 + ay * (o0 + s), cam, vp),
            ImpGizmo.WithAlpha(col, 70), col);
    }

    void DrawSelection(TCamera2D cam, TDimensions2 vp)
    {
        Vector2[] corners = new Vector2[4];
        for (int i = 0; i < _sel.Count; i++)
        {
            CompCorners(_sel[i], corners);
            Vector2 a = ImpGizmo.WorldToScreen(corners[0], cam, vp);
            Vector2 b = ImpGizmo.WorldToScreen(corners[1], cam, vp);
            Vector2 c = ImpGizmo.WorldToScreen(corners[2], cam, vp);
            Vector2 d = ImpGizmo.WorldToScreen(corners[3], cam, vp);
            Raylib.DrawLineEx(a, b, 1.4f, ImpGizmo.ColSelect);
            Raylib.DrawLineEx(b, c, 1.4f, ImpGizmo.ColSelect);
            Raylib.DrawLineEx(c, d, 1.4f, ImpGizmo.ColSelect);
            Raylib.DrawLineEx(d, a, 1.4f, ImpGizmo.ColSelect);
        }
    }
}
