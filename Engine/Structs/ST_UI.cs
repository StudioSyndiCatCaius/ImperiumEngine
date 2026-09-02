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


public struct TLayout2 //bad name. think of something better later
{
    [ImpVar] public EUIViewportAlignment align_H;
    [ImpVar] public EUIViewportAlignment align_V;
    [ImpVar] public Vector2 size=new(100,100);
    [ImpVar] public Vector2 size_min=Vector2.Zero;
    [ImpVar] public Vector2 size_max=Vector2.Zero;

    public TLayout2()
    {
        align_H = EUIViewportAlignment.Start;
        align_V = EUIViewportAlignment.Start;
    }
    
    // ===================================================================================================
    // Statics
    // ===================================================================================================

    public static TLayout2 NONE = new();
    
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
