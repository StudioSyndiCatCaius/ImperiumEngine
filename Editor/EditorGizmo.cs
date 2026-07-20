using System.Numerics;
using ImperiumEngine.Classes;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Editor;

public enum EGizmoMode
{
    Translate,
    Rotate,
    Scale,
}

public enum EGizmoSpace
{
    World,
    Local,
}

// transform gizmo for editing entities in the world viewport.
// Drawn in the raylib debug pass (after R3D.End). Two passes: a faded "x-ray" pass with depth
// testing off (so the gizmo is always visible on top), then a solid pass depth-tested against
// the scene — so any part buried inside a mesh shows as the faded version instead.
public class EditorGizmo
{
    //is this a 2d gizmo (for 2d/ui edit mode) — TODO: 2d widget editing
    bool is_2d;

    public EGizmoMode mode = EGizmoMode.Translate;
    public EGizmoSpace space = EGizmoSpace.World;

    //all entities the gizmo edits. Pivot is their centroid; local orientation
    //follows the primary (most recently selected, last in list)
    public List<ImpComponent3D> targets = new();

    ImpComponent3D? Primary => targets.Count > 0 ? targets[^1] : null;

    //set true on any frame a drag actually applies a transform; the world panel polls & clears
    //it to mark the level dirty. (A plain click that never drags leaves it false.)
    public bool made_edit;

    //true while the user is dragging a handle (camera/selection should not react to LMB)
    public bool IsDragging => _active != EHandle.None;

    //true while the mouse is over a handle (viewport click-picking should not steal the click)
    public bool IsHovered => _hover != EHandle.None;

    enum EHandle
    {
        None = -1,
        AxisX, AxisY, AxisZ, //arrow / ring / scale-bar handles per mode
        PlaneX, PlaneY, PlaneZ, //translate-only plane quads (named by their normal axis)
        Center, //uniform scale
    }

    EHandle _hover = EHandle.None;
    EHandle _active = EHandle.None;

    struct TargetState
    {
        public ImpComponent3D c;
        public Vector3 pos, scale;
        public Quaternion rot;
    }

    // drag state, captured on mouse press
    TargetState[] _drag_targets = [];
    Vector3 _drag_origin; //gizmo origin (pivot) at drag start
    Vector3 _drag_axis; //world-space axis of the grabbed handle
    float _drag_axis_t; //param along axis at drag start
    Vector3 _drag_plane_hit; //plane-handle hit point at drag start
    float _drag_angle; //ring angle at drag start
    Vector2 _drag_mouse; //mouse pos at drag start (center scale)

    // proportions, as fractions of the distance-based gizmo scale
    const float k_shaft_radius = 0.045f;
    const float k_shaft_start = 0.15f;
    const float k_shaft_end = 0.78f;
    const float k_tip_radius = 0.11f;
    const float k_plane_min = 0.28f;
    const float k_plane_max = 0.55f;
    const float k_ring_radius = 0.85f;
    const float k_ring_tube = 0.03f;
    const float k_cube_half = 0.09f;
    const float k_center_radius = 0.14f;
    const float k_pick_radius = 0.11f; //generous so handles are easy to grab

    static readonly Color s_col_x = new(226, 61, 61, 255);
    static readonly Color s_col_y = new(112, 208, 60, 255);
    static readonly Color s_col_z = new(58, 122, 236, 255);
    static readonly Color s_col_hot = new(255, 221, 64, 255);
    static readonly Color s_col_center = new(210, 210, 210, 255);

    // ----------------------------------------------------------------
    // Frame data (recomputed in Update, reused by Draw)
    // ----------------------------------------------------------------
    Vector3 _origin;
    float _scale; //world-space size, kept constant on screen
    Quaternion _orient;
    Vector3 _ax, _ay, _az; //oriented handle axes (world space)

    void RefreshFrame(Camera3D cam)
    {
        _origin = Vector3.Zero;
        foreach (var t in targets) _origin += t.WorldPosition;
        _origin /= targets.Count;

        _scale = Math.Max(0.001f, Vector3.Distance(cam.Position, _origin) * 0.16f);

        // scale always operates on local axes; translate/rotate follow the space toggle
        bool local = space == EGizmoSpace.Local || mode == EGizmoMode.Scale;
        if (local)
        {
            Primary!.GetWorldTRS(out _, out _orient, out _);
        }
        else
        {
            _orient = Quaternion.Identity;
        }

        _ax = Vector3.Transform(Vector3.UnitX, _orient);
        _ay = Vector3.Transform(Vector3.UnitY, _orient);
        _az = Vector3.Transform(Vector3.UnitZ, _orient);
    }

    Vector3 Axis(int i) => i == 0 ? _ax : i == 1 ? _ay : _az;

    // ----------------------------------------------------------------
    // Update: hover picking + dragging
    // ----------------------------------------------------------------

    // mouse_local: mouse position relative to the viewport image, view_size: displayed image size
    public void Update(Camera3D cam, Vector2 mouse_local, Vector2 view_size, bool can_interact)
    {
        if (targets.Count == 0 || is_2d) { _hover = _active = EHandle.None; return; }
        RefreshFrame(cam);

        if (view_size.X < 1 || view_size.Y < 1) return;
        Ray ray = GetScreenToWorldRayEx(mouse_local, cam, (int)view_size.X, (int)view_size.Y);

        if (_active != EHandle.None)
        {
            if (IsMouseButtonDown(MouseButton.Left)) UpdateDrag(ray, mouse_local);
            else _active = EHandle.None;
            return;
        }

        _hover = can_interact ? Pick(ray) : EHandle.None;

        if (_hover != EHandle.None && can_interact && IsMouseButtonPressed(MouseButton.Left))
            BeginDrag(_hover, ray, mouse_local);
    }

    EHandle Pick(Ray ray)
    {
        float pick_r = k_pick_radius * _scale;
        EHandle best = EHandle.None;
        float best_d = float.MaxValue;

        if (mode == EGizmoMode.Rotate)
        {
            // rings: sampled point-vs-ray test — robust even when the ring is edge-on
            for (int i = 0; i < 3; i++)
            {
                Vector3 n = Axis(i);
                OrthoBasis(n, out var u, out var v);
                float r = k_ring_radius * _scale;
                for (int s = 0; s < 64; s++)
                {
                    float a = s / 64f * MathF.Tau;
                    Vector3 p = _origin + (u * MathF.Cos(a) + v * MathF.Sin(a)) * r;
                    float d = RayPointDistance(ray, p);
                    if (d < pick_r && d < best_d) { best_d = d; best = EHandle.AxisX + i; }
                }
            }
            return best;
        }

        // center handle (uniform scale)
        if (mode == EGizmoMode.Scale)
        {
            float d = RayPointDistance(ray, _origin);
            if (d < k_center_radius * _scale) { best = EHandle.Center; best_d = d; }
        }

        // axis shafts+tips: segment-vs-ray distance
        for (int i = 0; i < 3; i++)
        {
            Vector3 a0 = _origin + Axis(i) * (k_shaft_start * _scale);
            Vector3 a1 = _origin + Axis(i) * _scale;
            float d = RaySegmentDistance(ray, a0, a1);
            if (d < pick_r && d < best_d) { best_d = d; best = EHandle.AxisX + i; }
        }

        // translate plane quads
        if (mode == EGizmoMode.Translate)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 n = Axis(i);
                Vector3 u = Axis((i + 1) % 3), v = Axis((i + 2) % 3);
                if (!RayPlane(ray, _origin, n, out Vector3 hit)) continue;
                float pu = Vector3.Dot(hit - _origin, u) / _scale;
                float pv = Vector3.Dot(hit - _origin, v) / _scale;
                if (pu < k_plane_min || pu > k_plane_max || pv < k_plane_min || pv > k_plane_max) continue;
                float d = Vector3.Distance(ray.Position, hit);
                if (d < best_d) { best_d = d; best = EHandle.PlaneX + i; }
            }
        }

        return best;
    }

    void BeginDrag(EHandle h, Ray ray, Vector2 mouse)
    {
        _active = h;
        _drag_origin = _origin;
        _drag_mouse = mouse;

        // snapshots are world-space; writes go back through SetWorldTRS
        _drag_targets = new TargetState[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].GetWorldTRS(out var pos, out var rot, out var scale);
            _drag_targets[i] = new TargetState { c = targets[i], pos = pos, rot = rot, scale = scale };
        }

        if (h >= EHandle.AxisX && h <= EHandle.AxisZ)
        {
            int i = h - EHandle.AxisX;
            _drag_axis = Axis(i);
            if (mode == EGizmoMode.Rotate)
                _drag_angle = RingAngle(ray, _drag_axis);
            else
                _drag_axis_t = RayAxisParam(ray, _drag_origin, _drag_axis);
        }
        else if (h >= EHandle.PlaneX && h <= EHandle.PlaneZ)
        {
            _drag_axis = Axis(h - EHandle.PlaneX); //plane normal
            RayPlane(ray, _drag_origin, _drag_axis, out _drag_plane_hit);
        }
    }

    void UpdateDrag(Ray ray, Vector2 mouse)
    {
        made_edit = true;   // reaching here means a handle is held and being dragged
        switch (mode)
        {
            case EGizmoMode.Translate:
            {
                Vector3 delta;
                if (_active >= EHandle.PlaneX)
                {
                    if (!RayPlane(ray, _drag_origin, _drag_axis, out Vector3 hit)) return;
                    delta = hit - _drag_plane_hit;
                }
                else
                {
                    float p = RayAxisParam(ray, _drag_origin, _drag_axis);
                    delta = _drag_axis * (p - _drag_axis_t);
                }
                foreach (ref var ts in _drag_targets.AsSpan())
                    ts.c.SetWorldTRS(ts.pos + delta, ts.rot, ts.scale);
                break;
            }

            case EGizmoMode.Rotate:
            {
                float delta = RingAngle(ray, _drag_axis) - _drag_angle;
                // world-space delta about the handle axis: compose each rotation with it,
                // and orbit each position around the shared pivot
                var qd = Quaternion.CreateFromAxisAngle(_drag_axis, delta);
                foreach (ref var ts in _drag_targets.AsSpan())
                {
                    ts.c.SetWorldTRS(
                        _drag_origin + Vector3.Transform(ts.pos - _drag_origin, qd),
                        Quaternion.Concatenate(ts.rot, qd),
                        ts.scale);
                }
                break;
            }

            case EGizmoMode.Scale:
                if (_active == EHandle.Center)
                {
                    // uniform: scale each entity and its offset from the pivot
                    float f = Math.Max(0.01f, 1f + (mouse.X - _drag_mouse.X) * 0.01f);
                    foreach (ref var ts in _drag_targets.AsSpan())
                        ts.c.SetWorldTRS(_drag_origin + (ts.pos - _drag_origin) * f, ts.rot, ts.scale * f);
                }
                else
                {
                    float p = RayAxisParam(ray, _drag_origin, _drag_axis);
                    float f = Math.Max(0.01f, MathF.Abs(_drag_axis_t) > 1e-5f ? p / _drag_axis_t : 1f);
                    int i = _active - EHandle.AxisX;
                    foreach (ref var ts in _drag_targets.AsSpan())
                    {
                        var s = ts.scale;
                        if (i == 0) s.X *= f; else if (i == 1) s.Y *= f; else s.Z *= f;
                        // stretch pivot offsets along the gizmo axis too
                        Vector3 off = ts.pos - _drag_origin;
                        off += _drag_axis * (Vector3.Dot(off, _drag_axis) * (f - 1f));
                        ts.c.SetWorldTRS(_drag_origin + off, ts.rot, s);
                    }
                }
                break;
        }
    }

    // angle of the ray's hit point around `axis` in the ring plane
    float RingAngle(Ray ray, Vector3 axis)
    {
        OrthoBasis(axis, out var u, out var v);
        Vector3 p;
        if (!RayPlane(ray, _drag_origin, axis, out p))
        {
            // ring edge-on: use the ray's closest approach to the origin instead
            float t = Math.Max(0, Vector3.Dot(_drag_origin - ray.Position, ray.Direction));
            p = ray.Position + ray.Direction * t;
        }
        Vector3 d = p - _drag_origin;
        return MathF.Atan2(Vector3.Dot(d, v), Vector3.Dot(d, u));
    }

    // ----------------------------------------------------------------
    // Drawing
    // ----------------------------------------------------------------

    public void Draw(Camera3D cam)
    {
        if (targets.Count == 0 || is_2d) return;
        RefreshFrame(cam);

        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableBackfaceCulling();

        // pass 1: faded, no depth test — always visible, even through geometry
        Rlgl.DisableDepthTest();
        DrawHandles(90);
        Rlgl.DrawRenderBatchActive();

        // pass 2: solid, depth-tested — overwrites the faded pass wherever the gizmo
        // is NOT occluded, so only buried parts keep the ghost look
        Rlgl.EnableDepthTest();
        DrawHandles(255);
        Rlgl.DrawRenderBatchActive();

        Rlgl.EnableBackfaceCulling();
    }

    Color HandleColor(EHandle h, int axis, byte alpha)
    {
        Color c = h == EHandle.Center ? s_col_center : axis == 0 ? s_col_x : axis == 1 ? s_col_y : s_col_z;
        EHandle hot = _active != EHandle.None ? _active : _hover;
        if (h == hot) c = s_col_hot;
        c.A = alpha;
        return c;
    }

    void DrawHandles(byte alpha)
    {
        switch (mode)
        {
            case EGizmoMode.Translate:
                for (int i = 0; i < 3; i++)
                {
                    DrawArrow(Axis(i), HandleColor(EHandle.AxisX + i, i, alpha));
                    DrawPlaneQuad(i, HandleColor(EHandle.PlaneX + i, i, (byte)(alpha / 2)));
                }
                break;

            case EGizmoMode.Rotate:
                for (int i = 0; i < 3; i++)
                    DrawRing(Axis(i), HandleColor(EHandle.AxisX + i, i, alpha));
                break;

            case EGizmoMode.Scale:
                for (int i = 0; i < 3; i++)
                    DrawScaleBar(Axis(i), HandleColor(EHandle.AxisX + i, i, alpha));
                DrawSphere(_origin, k_center_radius * _scale * 0.6f, HandleColor(EHandle.Center, -1, alpha));
                break;
        }
    }

    void DrawArrow(Vector3 axis, Color c)
    {
        Vector3 a0 = _origin + axis * (k_shaft_start * _scale);
        Vector3 a1 = _origin + axis * (k_shaft_end * _scale);
        Vector3 tip = _origin + axis * _scale;
        DrawCylinderEx(a0, a1, k_shaft_radius * _scale, k_shaft_radius * _scale, 10, c);
        DrawCylinderEx(a1, tip, k_tip_radius * _scale, 0f, 12, c); //cone head
    }

    void DrawScaleBar(Vector3 axis, Color c)
    {
        Vector3 a0 = _origin + axis * (k_shaft_start * _scale);
        Vector3 a1 = _origin + axis * ((1f - k_cube_half) * _scale);
        DrawCylinderEx(a0, a1, k_shaft_radius * _scale, k_shaft_radius * _scale, 10, c);
        DrawOrientedCube(_origin + axis * _scale, k_cube_half * _scale, c);
    }

    void DrawRing(Vector3 normal, Color c)
    {
        OrthoBasis(normal, out var u, out var v);
        float r = k_ring_radius * _scale;
        const int segs = 48;
        Vector3 prev = _origin + u * r;
        for (int s = 1; s <= segs; s++)
        {
            float a = s / (float)segs * MathF.Tau;
            Vector3 p = _origin + (u * MathF.Cos(a) + v * MathF.Sin(a)) * r;
            DrawCylinderEx(prev, p, k_ring_tube * _scale, k_ring_tube * _scale, 6, c);
            prev = p;
        }
    }

    void DrawPlaneQuad(int normal_axis, Color c)
    {
        Vector3 u = Axis((normal_axis + 1) % 3), v = Axis((normal_axis + 2) % 3);
        Vector3 p0 = _origin + (u * k_plane_min + v * k_plane_min) * _scale;
        Vector3 p1 = _origin + (u * k_plane_max + v * k_plane_min) * _scale;
        Vector3 p2 = _origin + (u * k_plane_max + v * k_plane_max) * _scale;
        Vector3 p3 = _origin + (u * k_plane_min + v * k_plane_max) * _scale;
        DrawTriangle3D(p0, p1, p2, c);
        DrawTriangle3D(p0, p2, p3, c);
    }

    // oriented cube from triangles (raylib's DrawCube is axis-aligned only)
    void DrawOrientedCube(Vector3 center, float half, Color c)
    {
        Vector3 x = _ax * half, y = _ay * half, z = _az * half;
        Span<Vector3> p = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
            p[i] = center + ((i & 1) != 0 ? x : -x) + ((i & 2) != 0 ? y : -y) + ((i & 4) != 0 ? z : -z);

        //winding is irrelevant: backface culling is off during gizmo draw
        ReadOnlySpan<int> idx = [0,1,3, 0,3,2, 4,7,5, 4,6,7,
                                 0,5,1, 0,4,5, 2,3,7, 2,7,6,
                                 0,2,6, 0,6,4, 1,5,7, 1,7,3];
        for (int i = 0; i < idx.Length; i += 3)
            DrawTriangle3D(p[idx[i]], p[idx[i + 1]], p[idx[i + 2]], c);
    }

    // ----------------------------------------------------------------
    // Math helpers
    // ----------------------------------------------------------------

    static void OrthoBasis(Vector3 n, out Vector3 u, out Vector3 v)
    {
        Vector3 refv = MathF.Abs(n.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitX;
        u = Vector3.Normalize(Vector3.Cross(refv, n));
        v = Vector3.Cross(n, u);
    }

    static bool RayPlane(Ray ray, Vector3 point, Vector3 normal, out Vector3 hit)
    {
        hit = default;
        float denom = Vector3.Dot(ray.Direction, normal);
        if (MathF.Abs(denom) < 1e-5f) return false;
        float t = Vector3.Dot(point - ray.Position, normal) / denom;
        if (t < 0) return false;
        hit = ray.Position + ray.Direction * t;
        return true;
    }

    static float RayPointDistance(Ray ray, Vector3 p)
    {
        float t = Math.Max(0, Vector3.Dot(p - ray.Position, ray.Direction));
        return Vector3.Distance(ray.Position + ray.Direction * t, p);
    }

    static float RaySegmentDistance(Ray ray, Vector3 a, Vector3 b)
    {
        Vector3 seg = b - a;
        float len = seg.Length();
        if (len < 1e-6f) return RayPointDistance(ray, a);
        Vector3 dir = seg / len;
        float t = RayAxisParam(ray, a, dir);
        t = Math.Clamp(t, 0, len);
        return RayPointDistance(ray, a + dir * t);
    }

    // param along the line (origin, axis) of its closest point to the ray
    static float RayAxisParam(Ray ray, Vector3 origin, Vector3 axis)
    {
        Vector3 r = origin - ray.Position;
        float b = Vector3.Dot(axis, ray.Direction);
        float d = Vector3.Dot(axis, r);
        float e = Vector3.Dot(ray.Direction, r);
        float denom = 1f - b * b;
        if (MathF.Abs(denom) < 1e-6f) return 0; //axis parallel to view ray
        return (b * e - d) / denom;
    }

}
