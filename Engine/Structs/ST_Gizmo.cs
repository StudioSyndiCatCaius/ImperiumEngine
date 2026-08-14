using System.Numerics;
using Raylib_cs;

namespace ImperiumEngine.Structs;

public enum ESceneEditorMode { Mode_3D, Mode_2D }

public enum EGizmoMode
{
    Translate,
    Rotate,
    Scale,
}

// Local = axes follow the (first) selected comp's orientation.
// World  = axes stay aligned to world XYZ.
public enum EGizmoSpace
{
    Local,
    World,
}

public enum EGizmoHandle
{
    None,
    AxisX,
    AxisY,
    AxisZ,
    PlaneXY,
    PlaneXZ,
    PlaneYZ,
    Screen,
    Uniform,
    Pivot,
}

public struct TCamera2D
{
    public Vector2 position;
    public float zoom;

    public TCamera2D()
    {
        position = Vector2.Zero;
        zoom = 1f;
    }
}

// Shared selection + edit state for the 2D and 3D scene-view gizmos.
// click to select imp. shift+click to select multiple. escape or click off to deselect.
public class TGizmoData
{
    public List<ImpComp> selected_comps = new List<ImpComp>();

    public EGizmoMode mode = EGizmoMode.Translate;
    public EGizmoSpace space = EGizmoSpace.World;

    // Kept for older call sites; mirrors `space == World`.
    public bool global_transform
    {
        get => space == EGizmoSpace.World;
        set => space = value ? EGizmoSpace.World : EGizmoSpace.Local;
    }

    // Snap steps applied while CTRL is held during a drag.
    public float snap_translate = 0.25f;
    public float snap_translate_2d = 8f;
    public float snap_rotate_deg = 15f;
    public float snap_scale = 0.1f;

    // True while the user is holding CTRL (set by the scene view each frame).
    public bool snap_active;

    public Action on_selection_changed;

    public void Selection_Clear()
    {
        if (selected_comps.Count == 0) return;
        selected_comps.Clear();
        on_selection_changed?.Invoke();
    }

    public void Selection_Set(IEnumerable<ImpComp> comps)
    {
        selected_comps.Clear();
        if (comps != null)
        {
            foreach (var c in comps)
            {
                if (c != null && !selected_comps.Contains(c)) selected_comps.Add(c);
            }
        }
        on_selection_changed?.Invoke();
    }

    public void Selection_Add(IEnumerable<ImpComp> comps)
    {
        if (comps == null) return;
        bool changed = false;
        foreach (var c in comps)
        {
            if (c == null || selected_comps.Contains(c)) continue;
            selected_comps.Add(c);
            changed = true;
        }
        if (changed) on_selection_changed?.Invoke();
    }

    public void Selection_Toggle(ImpComp comp)
    {
        if (comp == null) return;
        if (selected_comps.Contains(comp)) selected_comps.Remove(comp);
        else selected_comps.Add(comp);
        on_selection_changed?.Invoke();
    }

    public bool Selection_Contains(ImpComp comp) => selected_comps.Contains(comp);

    public ImpComp FirstSelected()
    {
        for (int i = 0; i < selected_comps.Count; i++)
            if (selected_comps[i] != null) return selected_comps[i];
        return null;
    }
}

public static class ImpGizmo
{
    public static readonly Color ColX = new(220, 55, 55, 255);
    public static readonly Color ColY = new(70, 190, 75, 255);
    public static readonly Color ColZ = new(55, 120, 230, 255);
    public static readonly Color ColHover = new(255, 220, 70, 255);
    public static readonly Color ColSelect = new(255, 170, 40, 255);
    public static readonly Color ColScreen = new(220, 220, 230, 255);

    public static readonly Color ColPivot = new(255, 130, 200, 255);

    public const float HitPx = 18f;
    public const float PivotPx = 7f;
    public const float AxisPx = 110f;
    public const float PlanePx = 28f;
    public const float LineThick = 5f;
    public const float ScaleBox = 8f;

    public static Color AxisColor(EGizmoHandle handle, EGizmoHandle hover, EGizmoHandle drag)
    {
        Color base_c = handle switch
        {
            EGizmoHandle.AxisX or EGizmoHandle.PlaneYZ => ColX,
            EGizmoHandle.AxisY or EGizmoHandle.PlaneXZ => ColY,
            EGizmoHandle.AxisZ or EGizmoHandle.PlaneXY => ColZ,
            EGizmoHandle.Uniform or EGizmoHandle.Screen => ColScreen,
            EGizmoHandle.Pivot => ColPivot,
            _ => ColScreen,
        };
        if (handle == hover || handle == drag) return ColHover;
        return base_c;
    }

    public static Color WithAlpha(Color c, byte a) => new(c.R, c.G, c.B, a);

    public static Color OccColor(Color c)
    {
        return new(
            (byte)(c.R * 0.55f + 30),
            (byte)(c.G * 0.55f + 30),
            (byte)(c.B * 0.55f + 30),
            (byte)Math.Clamp(c.A * 0.38f, 45, 95));
    }

    public static float Snap(float v, float step)
    {
        if (step <= 1e-8f) return v;
        return MathF.Round(v / step) * step;
    }

    public static Vector2 WorldToView(Vector2 world, TCamera2D cam, Vector2 view_size)
    {
        float z = cam.zoom <= 1e-6f ? 1f : cam.zoom;
        return view_size * 0.5f + (world - cam.position) * z;
    }

    public static Vector2 ViewToWorld(Vector2 view, TCamera2D cam, Vector2 view_size)
    {
        float z = cam.zoom <= 1e-6f ? 1f : cam.zoom;
        return cam.position + (view - view_size * 0.5f) / z;
    }

    public static Vector2 WorldToScreen(Vector2 world, TCamera2D cam, TDimensions2 vp)
    {
        return vp.position + WorldToView(world, cam, vp.size);
    }

    public static Vector2 ScreenToWorld(Vector2 screen, TCamera2D cam, TDimensions2 vp)
    {
        return ViewToWorld(screen - vp.position, cam, vp.size);
    }

    public static Vector2 WorldToScreen3(Vector3 world, Camera3D cam, TDimensions2 vp)
    {
        Vector2 s = Raylib.GetWorldToScreenEx(world, cam, (int)MathF.Max(1, vp.size.X), (int)MathF.Max(1, vp.size.Y));
        return vp.position + s;
    }

    public static Ray ScreenToRay3(Vector2 screen, Camera3D cam, TDimensions2 vp)
    {
        Vector2 local = screen - vp.position;
        int w = (int)MathF.Max(1, vp.size.X);
        int h = (int)MathF.Max(1, vp.size.Y);
        return Raylib.GetScreenToWorldRayEx(local, cam, w, h);
    }

    public static bool PointInFront(Vector3 world, Camera3D cam)
    {
        Vector3 fwd = cam.Target - cam.Position;
        if (fwd.LengthSquared() < 1e-10f) fwd = -Vector3.UnitZ;
        else fwd = Vector3.Normalize(fwd);
        return Vector3.Dot(world - cam.Position, fwd) > 0.02f;
    }

    public static float DistPointSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = Vector2.Dot(ab, ab);
        if (denom < 1e-8f) return Vector2.Distance(p, a);
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / denom, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    public static bool PointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return PointInTri(p, a, b, c) || PointInTri(p, a, c, d);
    }

    public static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s = a.Y * c.X - a.X * c.Y + (c.Y - a.Y) * p.X + (a.X - c.X) * p.Y;
        float t = a.X * b.Y - a.Y * b.X + (a.Y - b.Y) * p.X + (b.X - a.X) * p.Y;
        if ((s < 0) != (t < 0) && s != 0 && t != 0) return false;
        float u = -b.Y * c.X + a.Y * (c.X - b.X) + a.X * (b.Y - c.Y) + b.X * c.Y;
        return u < 0 ? (s <= 0 && s + t >= u) : (s >= 0 && s + t <= u);
    }

    public static void DrawArrow2(Vector2 a, Vector2 b, Color color, float thick = 5f)
    {
        Raylib.DrawLineEx(a, b, thick, color);
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 4f) return;
        d /= len;
        Vector2 n = new(-d.Y, d.X);
        float head = Math.Clamp(len * 0.26f, 12f, 20f);
        Vector2 tip = b;
        Vector2 base_p = b - d * head;
        Vector2 l = base_p + n * head * 0.5f;
        Vector2 r = base_p - n * head * 0.5f;
        Raylib.DrawTriangle(tip, l, r, color);
        Raylib.DrawTriangle(tip, r, l, color);
    }

    public static void DrawQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color fill, Color line)
    {
        Raylib.DrawTriangle(a, b, c, fill);
        Raylib.DrawTriangle(a, c, b, fill);
        Raylib.DrawTriangle(a, c, d, fill);
        Raylib.DrawTriangle(a, d, c, fill);
        Raylib.DrawLineEx(a, b, 2.2f, line);
        Raylib.DrawLineEx(b, c, 2.2f, line);
        Raylib.DrawLineEx(c, d, 2.2f, line);
        Raylib.DrawLineEx(d, a, 2.2f, line);
    }

    public static void DrawBox2(Vector2 center, float r, Color color)
    {
        Raylib.DrawRectangleV(center - new Vector2(r, r), new Vector2(r * 2, r * 2), color);
        Raylib.DrawRectangleLinesEx(new Rectangle(center.X - r, center.Y - r, r * 2, r * 2), 1.6f, Color.Black);
    }

    public static void DrawRing2(Vector2 center, float radius, Color color, float thick = 4.5f, int segs = 48)
    {
        if (radius < 2f) return;
        Vector2 prev = center + new Vector2(radius, 0);
        for (int i = 1; i <= segs; i++)
        {
            float a = i * (MathF.PI * 2f) / segs;
            Vector2 p = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
            Raylib.DrawLineEx(prev, p, thick, color);
            prev = p;
        }
    }

    public static void Rotate2(Vector2 v, float deg, out Vector2 o)
    {
        float rad = deg * (MathF.PI / 180f);
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        o = new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
