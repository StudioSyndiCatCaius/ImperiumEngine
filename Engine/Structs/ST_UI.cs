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

public struct TLayoutAlignment
{
    [ImpVar] public ELayoutAlignment align_H=ELayoutAlignment.Center;
    [ImpVar] public ELayoutAlignment align_V=ELayoutAlignment.Center;
    public TLayoutAlignment() {}
    
    public static TLayoutAlignment CENTER=new() { align_H=ELayoutAlignment.Center, align_V=ELayoutAlignment.Center };
    public static TLayoutAlignment FILL=new() { align_H=ELayoutAlignment.Fill, align_V=ELayoutAlignment.Fill };
    public static TLayoutAlignment TOP=new() { align_V=ELayoutAlignment.Start };
    public static TLayoutAlignment BOTTOM=new() { align_V=ELayoutAlignment.End };
    public static TLayoutAlignment LEFT=new() { align_H=ELayoutAlignment.Start };
    public static TLayoutAlignment RIGHT=new() { align_H=ELayoutAlignment.End };
    public static TLayoutAlignment TOP_LEFT=new() { align_H=ELayoutAlignment.Start, align_V=ELayoutAlignment.Start };
    public static TLayoutAlignment TOP_RIGHT=new() { align_H=ELayoutAlignment.End, align_V=ELayoutAlignment.Start };
    public static TLayoutAlignment BOTTOM_LEFT=new() { align_H=ELayoutAlignment.Start, align_V=ELayoutAlignment.End };
    public static TLayoutAlignment BOTTOM_RIGHT=new() { align_H=ELayoutAlignment.End, align_V=ELayoutAlignment.End };
    public static TLayoutAlignment FILL_TOP=new() { align_H=ELayoutAlignment.Fill, align_V=ELayoutAlignment.Start };
    public static TLayoutAlignment FILL_BOTTOM=new() { align_H=ELayoutAlignment.Fill, align_V=ELayoutAlignment.End };
    public static TLayoutAlignment FILL_LEFT=new() { align_H=ELayoutAlignment.Start, align_V=ELayoutAlignment.Fill };
    public static TLayoutAlignment FILL_RIGHT=new() { align_H=ELayoutAlignment.End, align_V=ELayoutAlignment.Fill };
    public static TLayoutAlignment FILL_CENTER_V=new() { align_H=ELayoutAlignment.Center, align_V=ELayoutAlignment.Fill };
    public static TLayoutAlignment FILL_CENTER_H=new() { align_H=ELayoutAlignment.Fill, align_V=ELayoutAlignment.Center };
}


public struct TLayout2 //Layout in 2d space. is relative to in input TBounds2 space
{
    [ImpVar] public TLayoutAlignment alignment = default;
    [ImpVar] public Vector2 position=Vector2.Zero; //position offset after alignment assinged
    [ImpVar] public Vector2 size=new(0,0);
    [ImpVar] public Vector2 size_min=Vector2.Zero;
    [ImpVar] public Vector2 size_max=Vector2.Zero;
    // Point on the output rect that alignment places. (0.5,0.5) + Center + window
    // puts the output center on the window center.
    [ImpVar] public Vector2 anchor_position=new(0.5f,0.5f);
    [ImpVar] public bool anchor_normalized=true;

    public TLayout2() { }

    public Vector2 GetSize()
    {
        if(size_min==Vector2.Zero && size_max==Vector2.Zero) return size;
        return Vector2.Clamp(size, size_min, size_max);
    }

    public Vector2 GetAnchorPosition()
    {
        return GetAnchorPosition(GetSize());
    }

    public Vector2 GetAnchorPosition(Vector2 resolved_size)
    {
        return anchor_normalized ? anchor_position * resolved_size : anchor_position;
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
        if (alignment.align_H == ELayoutAlignment.Fill) sz.X = view.X;
        if (alignment.align_V == ELayoutAlignment.Fill) sz.Y = view.Y;

        Vector2 anchor = GetAnchorPosition(sz);

        // Alignment places the ANCHOR in `outer`, not the top-left.
        static float Axis(ELayoutAlignment a, float viewStart, float viewSize, float offset, float anch)
            => a switch
            {
                ELayoutAlignment.Center => viewStart + viewSize * 0.5f + offset,
                ELayoutAlignment.End    => viewStart + viewSize - offset,
                // Fill: place the anchor so that after subtract the rect still covers the parent.
                ELayoutAlignment.Fill   => viewStart + anch + offset,
                _ => viewStart + offset,
            };

        Vector2 s = new(
            Axis(alignment.align_H, origin.X, view.X, position.X, anchor.X),
            Axis(alignment.align_V, origin.Y, view.Y, position.Y, anchor.Y));
        s -= anchor;

        return new TBounds2 { start = s, end = s + sz };
    }
    
    
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // Statics
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

    
    public static TLayout2 NONE = new();

    public static TLayout2 CENTER_BOX = new()
    {
        alignment = TLayoutAlignment.CENTER,
        size = new Vector2(400, 200),
    };
    
    public static TLayout2 H_BAR = new() //as a small horizontal bar
    {
        alignment = TLayoutAlignment.FILL_CENTER_H,
        size = new Vector2(20, 20),
    }; 
    public static TLayout2 V_BAR = new() //as a small vertical bar
    {
        alignment = TLayoutAlignment.FILL_CENTER_V,
        size = new Vector2(20, 20),
    };
    public static TLayout2 FULL = new()
    {
        alignment = TLayoutAlignment.FILL,
    };
}
