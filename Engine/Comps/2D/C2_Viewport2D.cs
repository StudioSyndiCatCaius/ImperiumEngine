using System.Numerics;
using Engine.Core;
using Raylib_cs;

namespace Engine.Comps._2D;

public class C2_Viewport2D : Imp2D
{
    public Camera2D camera=new ()
    {
    };
    
    public bool ViewportPos_IsValid(Vector2 screen_pos)
    {
        return false;
    }

    public Vector2 ViewportPos_ToLocalTransform(Vector2 screen_pos)
    {
        return default;
    }
}
    
