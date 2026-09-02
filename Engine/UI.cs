using System.Numerics;
using Engine.Interfaces;
using Engine.Structs;
using Raylib_cs;

namespace Engine;

public struct TSplitItem
{
    public float ratio;
    public Action draw;
}

// Immediate mode UI library
public static class UI
{
    private static TBounds2 default_bounds; // should be got from window size
    
    private static TBounds2 _DefBounds(TBounds2 space)
    {
        if (space.start==Vector2.Zero && space.end==Vector2.Zero) return default_bounds;
        return space;
    }
    
    public static TBounds2 Box(Color color, TLayout2 layout, TMargins margins = default, TBounds2 space = default)
    {
        TBounds2 _space = _DefBounds(space);
        TBounds2 region=TBounds2.Inset(space, margins);
        return region;
    }
    
    public static void Text(string text, TLayout2 layout, TMargins margins = default, TBounds2 space = default)
    {
        TBounds2 _space = _DefBounds(space);
    }

    public static void Split(TLayout2 layout, IEnumerable<TSplitItem> items, TMargins margins = default, TBounds2 space = default)
    {
        TBounds2 _space = _DefBounds(space);
    }

    public static void Button(string text, TLayout2 layout, TMargins margins = default, TBounds2 space = default)
    {
        TBounds2 _space = _DefBounds(space);
    }
}