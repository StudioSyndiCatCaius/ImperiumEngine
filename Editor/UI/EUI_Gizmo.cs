using System.Numerics;
using Editor.Windows;
using Engine.Core;
using Engine.Globals;
using Engine.Structs;
using ImGuiNET;
using Raylib_cs;

namespace Editor.UI;

enum GizmoHandle : byte
{
    None,
    X, Y, Z,
    XY, XZ, YZ,
    View,
    Uniform,
}

static class EdGizmo
{
    public const float Len = 84f;
    public const float Hit = 10f;
    public const float Head = 16f;

    public static readonly uint ColX = C(0.86f, 0.24f, 0.24f);
    public static readonly uint ColY = C(0.24f, 0.72f, 0.28f);
    public static readonly uint ColZ = C(0.26f, 0.46f, 0.90f);
    public static readonly uint ColW = C(0.92f, 0.86f, 0.28f);
    public static readonly uint ColSel = C(1f, 1f, 1f, 0.35f);

    public static uint C(float r, float g, float b, float a = 1f)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

    public static uint Axis(int i, bool hot)
    {
        uint c = i == 0 ? ColX : i == 1 ? ColY : ColZ;
        return hot ? ColW : c;
    }

    public static uint Fade(uint col, float a)
    {
        Vector4 v = ImGui.ColorConvertU32ToFloat4(col);
        v.W *= a;
        return ImGui.ColorConvertFloat4ToU32(v);
    }

    public static bool HitSeg(Vector2 a, Vector2 b, Vector2 p, float max, out float dist)
    {
        Vector2 ab = b - a;
        float l2 = ab.LengthSquared();
        float t = l2 < 1e-8f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
        dist = Vector2.Distance(p, a + ab * t);
        return dist <= max;
    }

    public static bool HitPoint(Vector2 a, Vector2 p, float max, out float dist)
    {
        dist = Vector2.Distance(a, p);
        return dist <= max;
    }

    public static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 v0 = c - a, v1 = b - a, v2 = p - a;
        float d00 = Vector2.Dot(v0, v0);
        float d01 = Vector2.Dot(v0, v1);
        float d02 = Vector2.Dot(v0, v2);
        float d11 = Vector2.Dot(v1, v1);
        float d12 = Vector2.Dot(v1, v2);
        float den = d00 * d11 - d01 * d01;
        if (MathF.Abs(den) < 1e-12f) return false;
        float inv = 1f / den;
        float u = (d11 * d02 - d01 * d12) * inv;
        float v = (d00 * d12 - d01 * d02) * inv;
        return u >= 0f && v >= 0f && u + v <= 1f;
    }

    public static bool PointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        => PointInTri(p, a, b, c) || PointInTri(p, a, c, d);

    public static void Arrow(ImDrawListPtr dl, Vector2 a, Vector2 b, uint col, float thick = 3f)
    {
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 4f) return;
        d /= len;
        Vector2 n = new(-d.Y, d.X);
        dl.AddLine(a, b - d * (Head - 2f), col, thick);
        dl.AddTriangleFilled(b, b - d * Head + n * 7f, b - d * Head - n * 7f, col);
    }

    public static void Quad(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector2 c, Vector2 d, uint fill, uint line)
    {
        dl.AddTriangleFilled(a, b, c, fill);
        dl.AddTriangleFilled(a, c, d, fill);
        dl.AddLine(a, b, line, 1.5f);
        dl.AddLine(b, c, line, 1.5f);
        dl.AddLine(c, d, line, 1.5f);
        dl.AddLine(d, a, line, 1.5f);
    }

    public static bool RayPlane(Ray ray, Vector3 plane_p, Vector3 plane_n, out Vector3 hit)
    {
        hit = default;
        float denom = Vector3.Dot(ray.Direction, plane_n);
        if (MathF.Abs(denom) < 1e-7f) return false;
        float t = Vector3.Dot(plane_p - ray.Position, plane_n) / denom;
        if (t < 0f) return false;
        hit = ray.Position + ray.Direction * t;
        return true;
    }

    public static Vector3 SafeNorm(Vector3 v, Vector3 fallback)
        => v.LengthSquared() < 1e-10f ? fallback : Vector3.Normalize(v);

    public static Vector2 SafeNorm(Vector2 v, Vector2 fallback)
        => v.LengthSquared() < 1e-10f ? fallback : Vector2.Normalize(v);

    public static void Basis(Vector3 axis, out Vector3 u, out Vector3 v)
    {
        Vector3 t = MathF.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        u = Vector3.Normalize(Vector3.Cross(axis, t));
        v = Vector3.Cross(axis, u);
    }
}

public class EUI_Gizmo3D
{
    public EEditorGizmo_Mode mode;
    public EEditorGizmo_Orientation orientation;
    public bool busy;
    public bool hovered;

    GizmoHandle hover;
    GizmoHandle active;
    Imp3D? target;
    TTransform3 start_local;
    TTransform3 start_global;
    Vector3 start_hit;
    Vector3 drag_axis;
    Vector3 drag_plane_n;
    Vector3 ax, ay, az, origin;
    float world_len;
    Vector2 start_mouse;

    public bool OnDraw(Imp3D? selected, Camera3D camera, Vector2 vp_min, int w, int h, bool item_hovered)
    {
        hovered = false;
        if (selected == null)
        {
            if (active != GizmoHandle.None) EndDrag();
            Cancel();
            busy = false;
            return false;
        }

        target = selected;
        origin = selected.global_transform.position;
        Vector3 fwd = SafeFwd(camera);
        Vector3 to = origin - camera.Position;
        if (Vector3.Dot(to, fwd) <= 0.05f)
        {
            if (active != GizmoHandle.None) EndDrag();
            busy = active != GizmoHandle.None;
            return busy;
        }

        bool local = orientation == EEditorGizmo_Orientation.Local || mode == EEditorGizmo_Mode.Scale;
        Quaternion q = local ? GMath.Euler_2_Quat(selected.global_transform.rotation) : Quaternion.Identity;
        ax = Vector3.Transform(Vector3.UnitX, q);
        ay = Vector3.Transform(Vector3.UnitY, q);
        az = Vector3.Transform(Vector3.UnitZ, q);
        world_len = MathF.Max(0.05f, Vector3.Distance(camera.Position, origin) * 0.14f);

        drag_vp_min = vp_min;
        drag_w = w;
        drag_h = h;

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(vp_min, vp_min + new Vector2(w, h), true);

        ImGuiIOPtr io = ImGui.GetIO();
        Vector2 mouse = io.MousePos;
        Ray ray = Raylib.GetScreenToWorldRayEx(mouse - vp_min, camera, w, h);
        bool alt = io.KeyAlt;

        if (active != GizmoHandle.None)
        {
            hover = active;
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) EndDrag();
            else Drag(selected, ray, mouse, camera);
        }
        else if (item_hovered && !alt)
        {
            hover = Pick(vp_min, camera, w, h, mouse);
            if (hover != GizmoHandle.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                BeginDrag(selected, ray, mouse, camera);
        }
        else hover = GizmoHandle.None;

        Draw(dl, camera, vp_min, w, h);
        dl.PopClipRect();

        hovered = hover != GizmoHandle.None;
        busy = active != GizmoHandle.None;
        return hovered || busy;
    }

    void BeginDrag(Imp3D o, Ray ray, Vector2 mouse, Camera3D camera)
    {
        drag_axis = AxisOf(hover);
        drag_plane_n = mode == EEditorGizmo_Mode.Rotate
            ? drag_axis
            : PlaneN(hover, SafeFwd(camera), camera.Up);
        if (!EdGizmo.RayPlane(ray, origin, drag_plane_n, out start_hit))
            start_hit = origin;
        start_local = o.transform;
        start_global = o.global_transform;
        start_mouse = mouse;
        active = hover;
    }

    void Drag(Imp3D o, Ray ray, Vector2 mouse, Camera3D camera)
    {
        switch (mode)
        {
            case EEditorGizmo_Mode.Translate:
                if (!EdGizmo.RayPlane(ray, start_global.position, drag_plane_n, out Vector3 hit)) return;
                Vector3 delta = hit - start_hit;
                if (active is GizmoHandle.X or GizmoHandle.Y or GizmoHandle.Z)
                    delta = drag_axis * Vector3.Dot(delta, drag_axis);
                o.Position_Set(start_global.position + delta, true);
                break;

            case EEditorGizmo_Mode.Rotate:
            {
                float ang;
                if (EdGizmo.RayPlane(ray, start_global.position, drag_axis, out Vector3 rh)
                    && (rh - start_global.position).LengthSquared() > 1e-8f
                    && (start_hit - start_global.position).LengthSquared() > 1e-8f
                    && MathF.Abs(Vector3.Dot(SafeFwd(camera), drag_axis)) > 0.12f)
                {
                    Vector3 a = Vector3.Normalize(start_hit - start_global.position);
                    Vector3 b = Vector3.Normalize(rh - start_global.position);
                    ang = MathF.Atan2(Vector3.Dot(Vector3.Cross(a, b), drag_axis), Vector3.Dot(a, b));
                }
                else if (Project(start_global.position, camera, drag_vp_min, drag_w, drag_h, out Vector2 pivot))
                {
                    float sign = Vector3.Dot(drag_axis, camera.Position - start_global.position) >= 0f ? 1f : -1f;
                    float a0 = MathF.Atan2(start_mouse.Y - pivot.Y, start_mouse.X - pivot.X);
                    float a1 = MathF.Atan2(mouse.Y - pivot.Y, mouse.X - pivot.X);
                    ang = (a1 - a0) * sign;
                }
                else return;
                Quaternion rot = Quaternion.CreateFromAxisAngle(drag_axis, ang);
                o.Rotation_Set(GMath.Quat_2_Euler(rot * GMath.Euler_2_Quat(start_global.rotation)), true);
                break;
            }

            case EEditorGizmo_Mode.Scale:
            {
                Vector3 ls = start_local.scale;
                if (active == GizmoHandle.Uniform)
                {
                    float f = 1f - (mouse.Y - start_mouse.Y) * 0.01f;
                    f = MathF.Max(0.001f, f);
                    o.Scale_Set(ls * f, false);
                    break;
                }
                if (!EdGizmo.RayPlane(ray, start_global.position, drag_plane_n, out Vector3 sh)) return;
                float mag = Vector3.Dot(sh - start_hit, drag_axis) / MathF.Max(0.001f, world_len);
                float fct = MathF.Max(0.001f, 1f + mag);
                if (active == GizmoHandle.X) ls.X = start_local.scale.X * fct;
                else if (active == GizmoHandle.Y) ls.Y = start_local.scale.Y * fct;
                else ls.Z = start_local.scale.Z * fct;
                o.Scale_Set(ls, false);
                break;
            }
        }
        if (o.scene != null) o.scene.is_dity = true;
    }

    Vector2 drag_vp_min;
    int drag_w, drag_h;

    void EndDrag()
    {
        if (target != null)
            Editor.history.Record(target, "transform", start_local, target.transform);
        active = GizmoHandle.None;
    }

    void Cancel()
    {
        active = GizmoHandle.None;
        hover = GizmoHandle.None;
        target = null;
    }

    GizmoHandle Pick(Vector2 vp_min, Camera3D camera, int w, int h, Vector2 mouse)
    {
        if (!Project(origin, camera, vp_min, w, h, out Vector2 o)) return GizmoHandle.None;
        GizmoHandle best = GizmoHandle.None;
        float best_d = EdGizmo.Hit;

        void Consider(GizmoHandle h, float d)
        {
            if (d < best_d) { best_d = d; best = h; }
        }

        Vector3 view = SafeFwd(camera);

        if (mode == EEditorGizmo_Mode.Rotate)
        {
            Vector3[] axes = { ax, ay, az };
            GizmoHandle[] hs = { GizmoHandle.X, GizmoHandle.Y, GizmoHandle.Z };
            for (int i = 0; i < 3; i++)
            {
                if (MathF.Abs(Vector3.Dot(axes[i], view)) < 0.04f) continue;
                RingPick(axes[i], camera, vp_min, w, h, mouse, o, out float d);
                if (d < best_d) { best_d = d; best = hs[i]; }
            }
            return best;
        }

        Vector2 ScreenDir(Vector3 axis, out bool ok)
        {
            ok = Project(origin + axis * world_len, camera, vp_min, w, h, out Vector2 p);
            if (!ok) return Vector2.Zero;
            Vector2 d = p - o;
            ok = d.LengthSquared() > 16f && MathF.Abs(Vector3.Dot(axis, view)) < 0.98f;
            return d;
        }

        bool ox = true, oy = true, oz = true;
        Vector2 dx = EdGizmo.SafeNorm(ScreenDir(ax, out ox), Vector2.UnitX);
        Vector2 dy = EdGizmo.SafeNorm(ScreenDir(ay, out oy), -Vector2.UnitY);
        Vector2 dz = EdGizmo.SafeNorm(ScreenDir(az, out oz), Vector2.UnitX);

        if (mode == EEditorGizmo_Mode.Translate)
        {
            if (ox && oy && PlanePick(o, dx, dy, mouse, out float dxy)) Consider(GizmoHandle.XY, dxy);
            if (ox && oz && PlanePick(o, dx, dz, mouse, out float dxz)) Consider(GizmoHandle.XZ, dxz);
            if (oy && oz && PlanePick(o, dy, dz, mouse, out float dyz)) Consider(GizmoHandle.YZ, dyz);
            if (EdGizmo.HitPoint(o, mouse, 8f, out float dc)) Consider(GizmoHandle.View, dc);
        }

        if (mode == EEditorGizmo_Mode.Scale && EdGizmo.HitPoint(o, mouse, 9f, out float du))
            Consider(GizmoHandle.Uniform, du);

        void AxisPick(bool on, Vector2 dir, GizmoHandle h)
        {
            if (!on) return;
            Vector2 tip = o + dir * EdGizmo.Len;
            if (mode == EEditorGizmo_Mode.Scale)
            {
                if (EdGizmo.HitPoint(tip, mouse, 9f, out float d)) Consider(h, d);
            }
            else if (EdGizmo.HitSeg(o, tip, mouse, EdGizmo.Hit, out float d))
                Consider(h, d);
        }
        AxisPick(ox, dx, GizmoHandle.X);
        AxisPick(oy, dy, GizmoHandle.Y);
        AxisPick(oz, dz, GizmoHandle.Z);
        return best;
    }

    static bool PlanePick(Vector2 o, Vector2 dx, Vector2 dy, Vector2 mouse, out float dist)
    {
        const float p = 26f, s = 20f;
        Vector2 a = o + dx * p + dy * p;
        Vector2 b = o + dx * (p + s) + dy * p;
        Vector2 c = o + dx * (p + s) + dy * (p + s);
        Vector2 d = o + dx * p + dy * (p + s);
        dist = 0f;
        if (!EdGizmo.PointInQuad(mouse, a, b, c, d)) return false;
        dist = 2f;
        return true;
    }

    void RingPick(Vector3 axis, Camera3D camera, Vector2 vp_min, int w, int h, Vector2 mouse, Vector2 o, out float dist)
    {
        dist = 999f;
        EdGizmo.Basis(axis, out Vector3 u, out Vector3 v);
        Vector3 view = EdGizmo.SafeNorm(camera.Position - origin, Vector3.UnitY);
        const int n = 48;
        Vector2 prev = default;
        bool has = false;
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * MathF.PI * 2f;
            Vector3 wp = origin + (u * MathF.Cos(a) + v * MathF.Sin(a)) * world_len;
            if (Vector3.Dot(wp - origin, view) < -0.02f) { has = false; continue; }
            if (!Project(wp, camera, vp_min, w, h, out Vector2 sp)) { has = false; continue; }
            if (has && EdGizmo.HitSeg(prev, sp, mouse, 8f, out float d) && d < dist) dist = d;
            prev = sp;
            has = true;
        }
    }

    void Draw(ImDrawListPtr dl, Camera3D camera, Vector2 vp_min, int w, int h)
    {
        if (!Project(origin, camera, vp_min, w, h, out Vector2 o)) return;
        Vector3 view = SafeFwd(camera);

        if (mode == EEditorGizmo_Mode.Rotate)
        {
            DrawRing(dl, ax, 0, camera, vp_min, w, h);
            DrawRing(dl, ay, 1, camera, vp_min, w, h);
            DrawRing(dl, az, 2, camera, vp_min, w, h);
            dl.AddCircleFilled(o, 4f, EdGizmo.C(1, 1, 1, 0.85f));
            return;
        }

        bool ScreenDir(Vector3 axis, out Vector2 dir)
        {
            dir = default;
            if (MathF.Abs(Vector3.Dot(axis, view)) > 0.98f) return false;
            if (!Project(origin + axis * world_len, camera, vp_min, w, h, out Vector2 p)) return false;
            Vector2 d = p - o;
            if (d.LengthSquared() < 16f) return false;
            dir = Vector2.Normalize(d);
            return true;
        }

        bool hx = ScreenDir(ax, out Vector2 dx);
        bool hy = ScreenDir(ay, out Vector2 dy);
        bool hz = ScreenDir(az, out Vector2 dz);

        if (mode == EEditorGizmo_Mode.Translate)
        {
            if (hx && hy) DrawPlane(dl, o, dx, dy, Hot(GizmoHandle.XY));
            if (hx && hz) DrawPlane(dl, o, dx, dz, Hot(GizmoHandle.XZ));
            if (hy && hz) DrawPlane(dl, o, dy, dz, Hot(GizmoHandle.YZ));
        }

        if (hx) DrawAxis(dl, o, dx, 0, Hot(GizmoHandle.X));
        if (hy) DrawAxis(dl, o, dy, 1, Hot(GizmoHandle.Y));
        if (hz) DrawAxis(dl, o, dz, 2, Hot(GizmoHandle.Z));

        uint oc = Hot(mode == EEditorGizmo_Mode.Scale ? GizmoHandle.Uniform : GizmoHandle.View)
            ? EdGizmo.ColW
            : EdGizmo.C(1, 1, 1, 0.9f);
        Vector2 cr = new(5.5f, 5.5f);
        dl.AddRectFilled(o - cr, o + cr, oc);
        dl.AddRect(o - cr, o + cr, EdGizmo.C(0, 0, 0, 0.55f));
    }

    bool Hot(GizmoHandle h) => hover == h || active == h;

    void DrawAxis(ImDrawListPtr dl, Vector2 o, Vector2 dir, int axis, bool hot)
    {
        uint col = EdGizmo.Axis(axis, hot);
        Vector2 tip = o + dir * EdGizmo.Len;
        if (mode == EEditorGizmo_Mode.Scale)
        {
            dl.AddLine(o, tip, col, hot ? 4f : 3f);
            Vector2 r = new(6f, 6f);
            dl.AddRectFilled(tip - r, tip + r, col);
            dl.AddRect(tip - r, tip + r, EdGizmo.C(0, 0, 0, 0.5f));
        }
        else EdGizmo.Arrow(dl, o, tip, col, hot ? 4f : 3f);
    }

    static void DrawPlane(ImDrawListPtr dl, Vector2 o, Vector2 dx, Vector2 dy, bool hot)
    {
        const float p = 26f, s = 20f;
        Vector2 a = o + dx * p + dy * p;
        Vector2 b = o + dx * (p + s) + dy * p;
        Vector2 c = o + dx * (p + s) + dy * (p + s);
        Vector2 d = o + dx * p + dy * (p + s);
        uint line = hot ? EdGizmo.ColW : EdGizmo.C(1, 1, 1, 0.7f);
        uint fill = hot ? EdGizmo.C(1f, 0.92f, 0.3f, 0.35f) : EdGizmo.C(1, 1, 1, 0.16f);
        EdGizmo.Quad(dl, a, b, c, d, fill, line);
    }

    void DrawRing(ImDrawListPtr dl, Vector3 axis, int axis_i, Camera3D camera, Vector2 vp_min, int w, int h)
    {
        bool hot = Hot(axis_i == 0 ? GizmoHandle.X : axis_i == 1 ? GizmoHandle.Y : GizmoHandle.Z);
        uint col = EdGizmo.Axis(axis_i, hot);
        EdGizmo.Basis(axis, out Vector3 u, out Vector3 v);
        Vector3 view = EdGizmo.SafeNorm(camera.Position - origin, Vector3.UnitY);
        const int n = 48;
        Vector2 prev = default;
        bool has = false;
        float thick = hot ? 3.4f : 2.2f;
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * MathF.PI * 2f;
            Vector3 wp = origin + (u * MathF.Cos(a) + v * MathF.Sin(a)) * world_len;
            float facing = Vector3.Dot(Vector3.Normalize(wp - origin), view);
            if (facing < -0.05f) { has = false; continue; }
            if (!Project(wp, camera, vp_min, w, h, out Vector2 sp)) { has = false; continue; }
            if (has)
            {
                uint c = facing < 0.15f ? EdGizmo.Fade(col, 0.35f) : col;
                dl.AddLine(prev, sp, c, thick);
            }
            prev = sp;
            has = true;
        }
    }

    Vector3 AxisOf(GizmoHandle h) => h switch
    {
        GizmoHandle.X or GizmoHandle.YZ => ax,
        GizmoHandle.Y or GizmoHandle.XZ => ay,
        _ => az,
    };

    Vector3 PlaneN(GizmoHandle h, Vector3 view, Vector3 up)
    {
        if (h == GizmoHandle.XY) return az;
        if (h == GizmoHandle.XZ) return ay;
        if (h == GizmoHandle.YZ) return ax;
        if (h == GizmoHandle.View || h == GizmoHandle.Uniform) return view;
        Vector3 a = AxisOf(h);
        Vector3 n = Vector3.Cross(a, Vector3.Cross(view, a));
        if (n.LengthSquared() < 1e-8f) n = Vector3.Cross(a, up);
        return EdGizmo.SafeNorm(n, up);
    }

    static Vector3 SafeFwd(Camera3D c)
    {
        Vector3 f = c.Target - c.Position;
        return f.LengthSquared() < 1e-8f ? Vector3.UnitX : Vector3.Normalize(f);
    }

    static bool Project(Vector3 p, Camera3D camera, Vector2 vp_min, int w, int h, out Vector2 s)
    {
        s = default;
        Vector3 f = camera.Target - camera.Position;
        if (f.LengthSquared() < 1e-8f) f = Vector3.UnitX;
        if (Vector3.Dot(p - camera.Position, Vector3.Normalize(f)) <= 0.02f) return false;
        Vector2 sc = Raylib.GetWorldToScreenEx(p, camera, w, h);
        if (float.IsNaN(sc.X) || float.IsNaN(sc.Y)) return false;
        s = vp_min + sc;
        return true;
    }
}

public class EUI_Gizmo2D
{
    public EEditorGizmo_Mode mode;
    public EEditorGizmo_Orientation orientation;
    public bool busy;
    public bool hovered;

    GizmoHandle hover;
    GizmoHandle active;
    Imp2D? target;
    TLayout2 start_layout;
    Vector2 start_world;
    Vector2 start_origin;
    Vector2 ax, ay, origin;
    float world_len;

    public bool OnDraw(Imp2D? selected, Camera2D camera, Vector2 vp_min, int w, int h, bool item_hovered)
    {
        hovered = false;
        if (selected == null)
        {
            if (active != GizmoHandle.None) EndDrag();
            active = GizmoHandle.None;
            hover = GizmoHandle.None;
            busy = false;
            return false;
        }

        target = selected;
        origin = selected.bounds.IsEmpty
            ? selected.layout.position
            : (selected.bounds.start + selected.bounds.end) * 0.5f;

        ax = Vector2.UnitX;
        ay = Vector2.UnitY;
        world_len = EdGizmo.Len / MathF.Max(0.001f, camera.Zoom);

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(vp_min, vp_min + new Vector2(w, h), true);

        ImGuiIOPtr io = ImGui.GetIO();
        Vector2 mouse = io.MousePos;
        Vector2 world = Raylib.GetScreenToWorld2D(mouse - vp_min, camera);
        bool alt = io.KeyAlt;

        if (active != GizmoHandle.None)
        {
            hover = active;
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) EndDrag();
            else Drag(selected, world);
        }
        else if (item_hovered && !alt)
        {
            hover = Pick(camera, vp_min, mouse);
            if (hover != GizmoHandle.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                BeginDrag(selected, world);
        }
        else hover = GizmoHandle.None;

        Draw(dl, camera, vp_min, selected);
        dl.PopClipRect();

        hovered = hover != GizmoHandle.None;
        busy = active != GizmoHandle.None;
        return hovered || busy;
    }

    void BeginDrag(Imp2D o, Vector2 world)
    {
        start_layout = o.layout;
        start_world = world;
        start_origin = origin;
        active = hover;
    }

    void Drag(Imp2D o, Vector2 world)
    {
        switch (mode)
        {
            case EEditorGizmo_Mode.Translate:
            {
                Vector2 delta = world - start_world;
                if (active == GizmoHandle.X) delta = ax * Vector2.Dot(delta, ax);
                else if (active == GizmoHandle.Y) delta = ay * Vector2.Dot(delta, ay);
                o.layout = start_layout;
                o.layout.position = start_layout.position + delta;
                o.bounds = o.Bounds_Cache();
                break;
            }
            case EEditorGizmo_Mode.Scale:
            {
                Vector2 sz = start_layout.size;
                if (sz.X < 1e-4f) sz.X = 1f;
                if (sz.Y < 1e-4f) sz.Y = 1f;
                o.layout = start_layout;
                if (active == GizmoHandle.Uniform)
                {
                    float d0 = MathF.Max(1f, Vector2.Distance(start_world, start_origin));
                    float d1 = Vector2.Distance(world, start_origin);
                    float f = MathF.Max(0.001f, d1 / d0);
                    o.layout.size = sz * f;
                }
                else
                {
                    Vector2 axis = active == GizmoHandle.X ? ax : ay;
                    float mag = Vector2.Dot(world - start_world, axis);
                    float fct = MathF.Max(0.001f, 1f + mag / MathF.Max(8f, world_len));
                    if (active == GizmoHandle.X) sz.X *= fct;
                    else sz.Y *= fct;
                    o.layout.size = sz;
                }
                o.bounds = o.Bounds_Cache();
                break;
            }
        }
        if (o.scene != null) o.scene.is_dity = true;
    }

    void EndDrag()
    {
        if (target != null)
            Editor.history.Record(target, "layout", start_layout, target.layout);
        active = GizmoHandle.None;
    }

    GizmoHandle Pick(Camera2D camera, Vector2 vp_min, Vector2 mouse)
    {
        Vector2 o = ToScreen(origin, camera, vp_min);
        Vector2 dx = ScreenAxis(ax, camera, vp_min, o);
        Vector2 dy = ScreenAxis(ay, camera, vp_min, o);
        GizmoHandle best = GizmoHandle.None;
        float best_d = EdGizmo.Hit;

        void Consider(GizmoHandle h, float d)
        {
            if (d < best_d) { best_d = d; best = h; }
        }

        if (mode == EEditorGizmo_Mode.Rotate)
        {
            float rad = EdGizmo.Len;
            float d = MathF.Abs(Vector2.Distance(mouse, o) - rad);
            return d <= 8f ? GizmoHandle.Z : GizmoHandle.None;
        }

        if (mode == EEditorGizmo_Mode.Translate)
        {
            const float p = 26f, s = 20f;
            Vector2 a = o + dx * p + dy * p;
            Vector2 b = o + dx * (p + s) + dy * p;
            Vector2 c = o + dx * (p + s) + dy * (p + s);
            Vector2 d = o + dx * p + dy * (p + s);
            if (EdGizmo.PointInQuad(mouse, a, b, c, d)) return GizmoHandle.XY;
            if (EdGizmo.HitPoint(o, mouse, 8f, out float dc)) Consider(GizmoHandle.XY, dc);
        }

        if (mode == EEditorGizmo_Mode.Scale && EdGizmo.HitPoint(o, mouse, 9f, out float du))
            Consider(GizmoHandle.Uniform, du);

        Vector2 tx = o + dx * EdGizmo.Len;
        Vector2 ty = o + dy * EdGizmo.Len;
        if (mode == EEditorGizmo_Mode.Scale)
        {
            if (EdGizmo.HitPoint(tx, mouse, 9f, out float dxh)) Consider(GizmoHandle.X, dxh);
            if (EdGizmo.HitPoint(ty, mouse, 9f, out float dyh)) Consider(GizmoHandle.Y, dyh);
        }
        else
        {
            if (EdGizmo.HitSeg(o, tx, mouse, EdGizmo.Hit, out float dxh)) Consider(GizmoHandle.X, dxh);
            if (EdGizmo.HitSeg(o, ty, mouse, EdGizmo.Hit, out float dyh)) Consider(GizmoHandle.Y, dyh);
        }
        return best;
    }

    void Draw(ImDrawListPtr dl, Camera2D camera, Vector2 vp_min, Imp2D o)
    {
        if (!o.bounds.IsEmpty)
        {
            Vector2 a = ToScreen(o.bounds.start, camera, vp_min);
            Vector2 b = ToScreen(new Vector2(o.bounds.end.X, o.bounds.start.Y), camera, vp_min);
            Vector2 c = ToScreen(o.bounds.end, camera, vp_min);
            Vector2 d = ToScreen(new Vector2(o.bounds.start.X, o.bounds.end.Y), camera, vp_min);
            dl.AddLine(a, b, EdGizmo.ColSel, 1.5f);
            dl.AddLine(b, c, EdGizmo.ColSel, 1.5f);
            dl.AddLine(c, d, EdGizmo.ColSel, 1.5f);
            dl.AddLine(d, a, EdGizmo.ColSel, 1.5f);
        }

        Vector2 so = ToScreen(origin, camera, vp_min);
        Vector2 dx = ScreenAxis(ax, camera, vp_min, so);
        Vector2 dy = ScreenAxis(ay, camera, vp_min, so);

        if (mode == EEditorGizmo_Mode.Rotate)
        {
            bool hot = hover == GizmoHandle.Z || active == GizmoHandle.Z;
            dl.AddCircle(so, EdGizmo.Len, hot ? EdGizmo.ColW : EdGizmo.ColZ, 48, hot ? 3.2f : 2.2f);
            dl.AddCircleFilled(so, 4f, EdGizmo.C(1, 1, 1, 0.9f));
            return;
        }

        if (mode == EEditorGizmo_Mode.Translate)
        {
            const float p = 26f, s = 20f;
            Vector2 a = so + dx * p + dy * p;
            Vector2 b = so + dx * (p + s) + dy * p;
            Vector2 c = so + dx * (p + s) + dy * (p + s);
            Vector2 d = so + dx * p + dy * (p + s);
            bool phot = hover == GizmoHandle.XY || active == GizmoHandle.XY;
            uint line = phot ? EdGizmo.ColW : EdGizmo.C(1, 1, 1, 0.7f);
            uint fill = phot ? EdGizmo.C(1f, 0.92f, 0.3f, 0.35f) : EdGizmo.C(1, 1, 1, 0.16f);
            EdGizmo.Quad(dl, a, b, c, d, fill, line);
        }

        DrawAxis2(dl, so, dx, 0, hover == GizmoHandle.X || active == GizmoHandle.X);
        DrawAxis2(dl, so, dy, 1, hover == GizmoHandle.Y || active == GizmoHandle.Y);

        bool uhot = hover == GizmoHandle.Uniform || active == GizmoHandle.Uniform
                    || (mode == EEditorGizmo_Mode.Translate && (hover == GizmoHandle.View || active == GizmoHandle.View));
        uint oc = uhot ? EdGizmo.ColW : EdGizmo.C(1, 1, 1, 0.9f);
        Vector2 cr = new(5.5f, 5.5f);
        dl.AddRectFilled(so - cr, so + cr, oc);
        dl.AddRect(so - cr, so + cr, EdGizmo.C(0, 0, 0, 0.55f));
    }

    void DrawAxis2(ImDrawListPtr dl, Vector2 o, Vector2 dir, int axis, bool hot)
    {
        uint col = EdGizmo.Axis(axis, hot);
        Vector2 tip = o + dir * EdGizmo.Len;
        if (mode == EEditorGizmo_Mode.Scale)
        {
            dl.AddLine(o, tip, col, hot ? 4f : 3f);
            Vector2 r = new(6f, 6f);
            dl.AddRectFilled(tip - r, tip + r, col);
            dl.AddRect(tip - r, tip + r, EdGizmo.C(0, 0, 0, 0.5f));
        }
        else EdGizmo.Arrow(dl, o, tip, col, hot ? 4f : 3f);
    }

    static Vector2 ToScreen(Vector2 world, Camera2D camera, Vector2 vp_min)
        => vp_min + Raylib.GetWorldToScreen2D(world, camera);

    Vector2 ScreenAxis(Vector2 axis, Camera2D camera, Vector2 vp_min, Vector2 o)
    {
        Vector2 p = ToScreen(origin + axis * 64f, camera, vp_min);
        return EdGizmo.SafeNorm(p - o, axis);
    }
}
