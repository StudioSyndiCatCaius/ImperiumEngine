using System.Numerics;
using Engine.Assets;
using Engine.Enums;
using Raylib_cs;

namespace Engine.Structs;




public struct TMargins
{
    [ImpVar] public float left, right, top, bottom;
    
    public TMargins(float left, float right, float top, float bottom)
    {
        this.left = left;
        this.right = right;
        this.top = top;
        this.bottom = bottom;
    }
}


public struct TLayout2 //Layout in 2d space. is relative to in input TBounds2 space
{
    [ImpVar] public EUIViewportAlignment align_H=EUIViewportAlignment.Center;
    [ImpVar] public EUIViewportAlignment align_V=EUIViewportAlignment.Center;
    [ImpVar] public Vector2 position=Vector2.Zero; //position offset after alignment assinged
    [ImpVar] public Vector2 size=new(100,100);
    [ImpVar] public Vector2 size_min=Vector2.Zero;
    [ImpVar] public Vector2 size_max=Vector2.Zero;
    [ImpVar] public Vector2 anchor_position=new(0.5f,0.5f);
    [ImpVar] public bool anchor_normalized=true;

    public TLayout2()
    {
        align_H = EUIViewportAlignment.Start;
        align_V = EUIViewportAlignment.Start;
    }

    public Vector2 GetSize()
    {
        if(size_min==Vector2.Zero && size_max==Vector2.Zero) return size;
        return Vector2.Clamp(size, size_min, size_max);
    }

    public Vector2 GetAnchorPosition()
    {
        return anchor_normalized ? anchor_position * size : anchor_position;
    }

    public TBounds2 MakeBounds(TBounds2 outer=default)
    {
        if (outer.IsEmpty) outer = TBounds2.GetWindowBounds(); //when no outer bounds is given, use the entire window space
        Vector2 origin = new(
            MathF.Min(outer.start.X, outer.end.X),
            MathF.Min(outer.start.Y, outer.end.Y));
        Vector2 view = new(
            MathF.Abs(outer.end.X - outer.start.X),
            MathF.Abs(outer.end.Y - outer.start.Y));

        Vector2 sz = GetSize();
        if (align_H == EUIViewportAlignment.Fill) sz.X = view.X;
        if (align_V == EUIViewportAlignment.Fill) sz.Y = view.Y;

        static float Axis(EUIViewportAlignment a, float viewStart, float viewSize, float self, float offset)
            => a switch
            {
                EUIViewportAlignment.Center => viewStart + (viewSize - self) * 0.5f + offset,
                EUIViewportAlignment.End    => viewStart + viewSize - self - offset,
                // Start and Fill: pin to the start edge; Fill already stretched size
                _ => viewStart + offset,
            };

        Vector2 s = new(
            Axis(align_H, origin.X, view.X, sz.X, position.X),
            Axis(align_V, origin.Y, view.Y, sz.Y, position.Y));

        // Anchor: position refers to this point on the widget, not top-left.
        // Use the resolved size (sz), not the raw `size` field, after Fill/clamp.
        Vector2 anchor = anchor_normalized ? anchor_position * sz : anchor_position;
        s -= anchor;

        return new TBounds2 { start = s, end = s + sz };
    }
    
    
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // Statics
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    
    public static TLayout2 NONE = new();

    public static TLayout2 CENTER_BOX = new()
    {
        align_V = EUIViewportAlignment.Center,
        align_H = EUIViewportAlignment.Center,
        size = new Vector2(500, 300),
    };
    
    public static TLayout2 H_BAR = new() //as a small horizontal bar
    {
        align_V = EUIViewportAlignment.Start,
        align_H = EUIViewportAlignment.Fill,
        size = new Vector2(20, 20),
    }; 
    public static TLayout2 V_BAR = new() //as a small horizontal bar
    {
        align_V = EUIViewportAlignment.Fill,
        align_H = EUIViewportAlignment.Start,
        size = new Vector2(20, 20),
    };
    public static TLayout2 FULL = new()
    {
        align_V = EUIViewportAlignment.Fill,
        align_H = EUIViewportAlignment.Fill,
    };
}
