using System.Numerics;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Interfaces;

namespace Engine.Structs;

// ======================================================================================================
// 3D
// ======================================================================================================

public struct TTransform3 : I_Property
{
    [ImpVar] public Vector3 position;
    [ImpVar] public Vector3 rotation; //euler degrees, applied yaw/pitch/roll - see Imp3D
    [ImpVar] public Vector3 scale=Vector3.One;

    public TTransform3()
    {
        position = default;
        rotation = default;
        scale = Vector3.One;
    }


    // -------------------------------------------------
    // Static
    // -------------------------------------------------
    public static TTransform3 Add(TTransform3 a,TTransform3 b, bool scale = false)
    {
        Vector3 local_pos = b.position;
        if (scale)
            local_pos = new Vector3(local_pos.X * a.scale.X, local_pos.Y * a.scale.Y, local_pos.Z * a.scale.Z);
        TTransform3 output = new();
        output.position = GMath.V3_Offset(a.position, local_pos, a.rotation);
        output.rotation = GMath.Quat_2_Euler(GMath.Euler_2_Quat(a.rotation) * GMath.Euler_2_Quat(b.rotation));
        output.scale = a.scale * b.scale;
        return output;
    }

    public static TTransform3 Subtract(TTransform3 a,TTransform3 b, bool scale = false)
    {
        Quaternion inv = Quaternion.Inverse(GMath.Euler_2_Quat(b.rotation));
        Vector3 local_pos = Vector3.Transform(a.position - b.position, inv);
        if (scale)
            local_pos = new Vector3(
                b.scale.X != 0 ? local_pos.X / b.scale.X : local_pos.X,
                b.scale.Y != 0 ? local_pos.Y / b.scale.Y : local_pos.Y,
                b.scale.Z != 0 ? local_pos.Z / b.scale.Z : local_pos.Z);
        TTransform3 output = new();
        output.position = local_pos;
        output.rotation = GMath.Quat_2_Euler(inv * GMath.Euler_2_Quat(a.rotation));
        output.scale = new Vector3(
            b.scale.X != 0 ? a.scale.X / b.scale.X : a.scale.X,
            b.scale.Y != 0 ? a.scale.Y / b.scale.Y : a.scale.Y,
            b.scale.Z != 0 ? a.scale.Z / b.scale.Z : a.scale.Z);
        return output;
    }

    public static TTransform3 Offset(TTransform3 t, Vector3 offset)
    {
        TTransform3 result = t; // carry rotation/scale through; only the position is offset
        result.position=GMath.V3_Offset(t.position,offset,t.rotation);
        return result;
    }
}

public struct TBounds3
{
    public Vector3 center;
    public Vector3 size;
    public Vector3 rotation;

    public bool IsEmpty
    {
        get
        {
            return MathF.Abs(size.X) + MathF.Abs(size.Y) + MathF.Abs(size.Z) <= 1e-8f;
        }
    }

    public bool IsPointInside(Vector3 point)
    {
        Vector3 d = point - center;
        if (MathF.Abs(rotation.X) > 1e-4f || MathF.Abs(rotation.Y) > 1e-4f || MathF.Abs(rotation.Z) > 1e-4f)
        {
            d = Vector3.Transform(d, Quaternion.Inverse(GMath.Euler_2_Quat(rotation)));
        }
        Vector3 h = new(MathF.Abs(size.X) * 0.5f, MathF.Abs(size.Y) * 0.5f, MathF.Abs(size.Z) * 0.5f);
        return d.X >= -h.X && d.Y >= -h.Y && d.Z >= -h.Z
            && d.X < h.X && d.Y < h.Y && d.Z < h.Z;
    }

    public bool Raycast(Vector3 origin, Vector3 dir, out float t)
    {
        t = 0;
        if (IsEmpty) return false;
        float len2 = dir.LengthSquared();
        if (len2 < 1e-16f) return false;

        Quaternion inv = Quaternion.Inverse(GMath.Euler_2_Quat(rotation));
        Vector3 o = Vector3.Transform(origin - center, inv);
        Vector3 d = Vector3.Transform(dir, inv);
        Vector3 h = new(MathF.Abs(size.X) * 0.5f, MathF.Abs(size.Y) * 0.5f, MathF.Abs(size.Z) * 0.5f);

        float tmin = float.NegativeInfinity;
        float tmax = float.PositiveInfinity;
        for (int i = 0; i < 3; i++)
        {
            float oi = i == 0 ? o.X : i == 1 ? o.Y : o.Z;
            float di = i == 0 ? d.X : i == 1 ? d.Y : d.Z;
            float hi = i == 0 ? h.X : i == 1 ? h.Y : h.Z;
            if (MathF.Abs(di) < 1e-8f)
            {
                if (oi < -hi || oi > hi) return false;
                continue;
            }
            float inv_d = 1f / di;
            float t1 = (-hi - oi) * inv_d;
            float t2 = (hi - oi) * inv_d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (t1 > tmin) tmin = t1;
            if (t2 < tmax) tmax = t2;
            if (tmin > tmax) return false;
        }
        if (tmax < 0f) return false;
        t = tmin < 0f ? 0f : tmin;
        return true;
    }

    public void Corners(Span<Vector3> corners)
    {
        if (corners.Length < 8)
        {
            return;
        }
        Vector3 h = new(
            MathF.Abs(size.X) * 0.5f,
            MathF.Abs(size.Y) * 0.5f,
            MathF.Abs(size.Z) * 0.5f);
        Quaternion q = GMath.Euler_2_Quat(rotation);
        Vector3 x = Vector3.Transform(new Vector3(h.X, 0f, 0f), q);
        Vector3 y = Vector3.Transform(new Vector3(0f, h.Y, 0f), q);
        Vector3 z = Vector3.Transform(new Vector3(0f, 0f, h.Z), q);
        corners[0] = center - x - y - z;
        corners[1] = center + x - y - z;
        corners[2] = center - x + y - z;
        corners[3] = center + x + y - z;
        corners[4] = center - x - y + z;
        corners[5] = center + x - y + z;
        corners[6] = center - x + y + z;
        corners[7] = center + x + y + z;
    }
    
    // ------------------------------------------------
    // STATIC
    // ------------------------------------------------
    public static TBounds3 Merge(Span<TBounds3> bounds)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool any = false;
        TBounds3 only = ZERO;
        int count = 0;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < bounds.Length; i++)
        {
            TBounds3 b = bounds[i];
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
            only = b;
            count++;
            any = true;
        }
        if (!any)
        {
            return ZERO;
        }
        if (count == 1)
        {
            return only;
        }
        return new TBounds3
        {
            center = (min + max) * 0.5f,
            size = max - min,
            rotation = Vector3.Zero,
        };
    }

    public static TBounds3 Offset(TBounds3 bounds, TTransform3 t, bool scale = false)
    {
        if (bounds.IsEmpty) return ZERO;
        Vector3 local = bounds.center;
        Vector3 size = bounds.size;
        if (scale)
        {
            local = new Vector3(local.X * t.scale.X, local.Y * t.scale.Y, local.Z * t.scale.Z);
            size = new Vector3(
                MathF.Abs(size.X * t.scale.X),
                MathF.Abs(size.Y * t.scale.Y),
                MathF.Abs(size.Z * t.scale.Z));
        }
        Quaternion q = GMath.Euler_2_Quat(t.rotation);
        return new TBounds3
        {
            center = t.position + Vector3.Transform(local, q),
            size = size,
            rotation = GMath.Quat_2_Euler(q * GMath.Euler_2_Quat(bounds.rotation)),
        };
    }

    public static TBounds3 ZERO = new() { center = Vector3.Zero, size = Vector3.Zero, rotation = Vector3.Zero, };
}


// ---------------------------------------------------------------
// Int Vector
// ---------------------------------------------------------------
public struct TVector3i
{
    public int x;
    public int y;
    public int z;
    
    public TVector3i(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
}

// ====================================================================================================
// 2D
// ======================================================================================================

// ---------------------------------------------------------------
// Transform
// ---------------------------------------------------------------

public struct TTransform2 : I_Property
{
    [ImpVar] public Vector2 position;
    [ImpVar] public double rotation; //degrees
    [ImpVar] public Vector2 scale=Vector2.One;

    public TTransform2()
    {
        position = default;
        rotation = 0;
        scale = Vector2.One;
    }
    
    // -------------------------------------------------
    // Static
    // -------------------------------------------------
    public static TTransform2 Add(TTransform2 a,TTransform2 b, bool scale = false)
    {
        TTransform2 output = new();
        output.position = a.position + b.position;
        output.rotation = a.rotation + b.rotation;
        if (scale) output.scale = a.scale + b.scale;
        return output;
    }
    
    public static TTransform2 Subtract(TTransform2 a,TTransform2 b, bool scale = false)
    {
        TTransform2 output = new();
        output.position = a.position - b.position;
        output.rotation = a.rotation - b.rotation;
        if (scale) output.scale = a.scale - b.scale;
        return output;
    }
}

// ---------------------------------------------------------------
// Int Vector
// ---------------------------------------------------------------
public struct TVector2i
{
    public int x;
    public int y;

    public TVector2i(int x, int y) { this.x = x; this.y = y; }
    
    // -------------------------------------------
    // Static
    // -------------------------------------------
    public static TVector2i p720 =new(720, 480);
    public static TVector2i p1080 =new(1080, 720);
    public static TVector2i p1440 =new(1440, 900);
    public static TVector2i p1920 =new(1920, 1080);
    public static TVector2i p2560 =new(2560, 1440);
    public static TVector2i p3840 =new(3840, 2160);
}

// ---------------------------------------------------------------
// Bounds
// ---------------------------------------------------------------

public struct TBounds2
{
    public Vector2 start;
    public Vector2 end;

    public bool IsEmpty => MathF.Abs(end.X - start.X) + MathF.Abs(end.Y - start.Y) <= 1e-8f;
    

    // `this` is the parent/content/slot rect.
    public TBounds2 FromLayout(Vector2 position, TLayout2 layout)
    {
        Vector2 origin = new(
            MathF.Min(start.X, end.X),
            MathF.Min(start.Y, end.Y));
        Vector2 view = new(
            MathF.Abs(end.X - start.X),
            MathF.Abs(end.Y - start.Y));

        Vector2 size = layout.size;
        if (layout.size_max != Vector2.Zero)
            size = Vector2.Clamp(size, layout.size_min, layout.size_max);
        else
            size = Vector2.Max(size, layout.size_min);

        if (layout.align_H == EUIViewportAlignment.Fill) size.X = view.X;
        if (layout.align_V == EUIViewportAlignment.Fill) size.Y = view.Y;

        float Axis(EUIViewportAlignment a, float view_start, float view_size, float self, float offset) => a switch
        {
            EUIViewportAlignment.Center => view_start + (view_size - self) * 0.5f + offset,
            EUIViewportAlignment.End => view_start + view_size - self - offset,
            _ => view_start + offset,
        };

        Vector2 s = new(
            Axis(layout.align_H, origin.X, view.X, size.X, position.X),
            Axis(layout.align_V, origin.Y, view.Y, size.Y, position.Y));
        return new TBounds2 { start = s, end = s + size };
    }
    
    public Vector2 Center(TBounds2 bounds) { return (start + end) / 2; }
    
    public bool IsPointInside(Vector2 point)
    {
        float min_x = MathF.Min(start.X, end.X);
        float max_x = MathF.Max(start.X, end.X);
        float min_y = MathF.Min(start.Y, end.Y);
        float max_y = MathF.Max(start.Y, end.Y);
        return point.X >= min_x && point.Y >= min_y
            && point.X < max_x && point.Y < max_y;
    }

    public TBounds2 Expand(TMargins margins)
    {
        return new TBounds2
        {
            start = start - new Vector2(margins.left, margins.top),
            end = end + new Vector2(margins.right, margins.bottom),
        };
    }

    public TBounds2[] Split(float[] sizes, bool as_ratio = false, EUIOrentation orientation = EUIOrentation.H,TMargins margins=default)
    {
        if (sizes == null || sizes.Length == 0)
            return Array.Empty<TBounds2>();

        int n = sizes.Length;
        TBounds2[] result = new TBounds2[n];
        float sx = start.X, sy = start.Y, ex = end.X, ey = end.Y;
        bool horiz = orientation == EUIOrentation.H;
        float cursor = horiz ? sx : sy;
        float mul = as_ratio ? (horiz ? ex - sx : ey - sy) : 1f;
        float ml = margins.left, mr = margins.right, mt = margins.top, mb = margins.bottom;
        for (int i = 0; i < n; i++)
        {
            float next = cursor + sizes[i] * mul;
            float x0, y0, x1, y1;
            if (horiz)
            {
                x0 = cursor + ml;
                y0 = sy + mt;
                x1 = next - mr;
                y1 = ey - mb;
            }
            else
            {
                x0 = sx + ml;
                y0 = cursor + mt;
                x1 = ex - mr;
                y1 = next - mb;
            }
            if (x1 < x0) x1 = x0;
            if (y1 < y0) y1 = y0;
            result[i] = new TBounds2 { start = new Vector2(x0, y0), end = new Vector2(x1, y1) };
            cursor = next;
        }
        return result;
    }
    
    public TBounds2 Offset(Vector2 offset) { return new TBounds2 { start = start + offset, end = end + offset }; }

    // ========================================================================================================
    // STATIC
    // ========================================================================================================

    public static TBounds2 GetWindowBounds()
    {
        // Full app window / screen space (kept in sync by App.RefreshWindow).
        return App.viewport_main.Bounds;
    }
    
    public static TBounds2 Inset(TBounds2 b, TMargins m)
    {
        float x0 = MathF.Min(b.start.X, b.end.X) + m.left;
        float y0 = MathF.Min(b.start.Y, b.end.Y) + m.top;
        float x1 = MathF.Max(b.start.X, b.end.X) - m.right;
        float y1 = MathF.Max(b.start.Y, b.end.Y) - m.bottom;
        if (x1 < x0) x1 = x0;
        if (y1 < y0) y1 = y0;
        return new TBounds2 { start = new Vector2(x0, y0), end = new Vector2(x1, y1) };
    }
}
