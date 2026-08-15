using System.Numerics;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

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

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty prop_ui)
    {
        prop_ui.Group_BuildNamed(nameof(position), nameof(rotation), nameof(scale));
    }
}

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

    public bool Inspector_IsCustom() => true;

    public void Inspector_Rebuild(C2_InspectorProperty prop_ui)
    {
        prop_ui.Group_BuildNamed(nameof(position), nameof(rotation), nameof(scale));
    }
}

public struct TBounds3
{
    public Vector3 center;
    public Vector3 size;
    public Vector3 rotation;

    public bool IsPointInside(Vector3 point)
    {
        Vector3 d = point - center;
        if (MathF.Abs(rotation.X) > 1e-4f || MathF.Abs(rotation.Y) > 1e-4f || MathF.Abs(rotation.Z) > 1e-4f)
            d = Vector3.Transform(d, Quaternion.Inverse(ImpMath.EulerToQuat(rotation)));
        Vector3 h = new(MathF.Abs(size.X) * 0.5f, MathF.Abs(size.Y) * 0.5f, MathF.Abs(size.Z) * 0.5f);
        return d.X >= -h.X && d.Y >= -h.Y && d.Z >= -h.Z
            && d.X < h.X && d.Y < h.Y && d.Z < h.Z;
    }
    
}


public struct TBounds2
{
    public Vector2 start;
    public Vector2 end;


    public TBounds2(Vector2 position, TLayout2 layout, TBounds2 parent)
    {
        Vector2 origin = new(
            MathF.Min(parent.start.X, parent.end.X),
            MathF.Min(parent.start.Y, parent.end.Y));
        Vector2 view = new(
            MathF.Abs(parent.end.X - parent.start.X),
            MathF.Abs(parent.end.Y - parent.start.Y));

        Vector2 size = layout.size;
        if (layout.size_max != Vector2.Zero)
            size = Vector2.Clamp(size, layout.size_min, layout.size_max);
        else
            size = Vector2.Max(size, layout.size_min);

        if (layout.orient_H == EUIViewportAlignment.Fill) size.X = view.X;
        if (layout.orient_V == EUIViewportAlignment.Fill) size.Y = view.Y;

        float Axis(EUIViewportAlignment a, float view_start, float view_size, float self, float offset) => a switch
        {
            EUIViewportAlignment.Center => view_start + (view_size - self) * 0.5f + offset,
            EUIViewportAlignment.End => view_start + view_size - self - offset,
            _ => view_start + offset,
        };

        start = new Vector2(
            Axis(layout.orient_H, origin.X, view.X, size.X, position.X),
            Axis(layout.orient_V, origin.Y, view.Y, size.Y, position.Y));
        end = start + size;
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

}
