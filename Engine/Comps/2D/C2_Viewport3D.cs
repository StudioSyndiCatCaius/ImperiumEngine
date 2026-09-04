using System.Numerics;
using Engine.Core;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Comps._2D;

public class C2_Viewport3D : Imp2D
{
    public Camera3D camera=new Camera3D()
    {
        
    };

    public bool ViewportPos_IsValid(Vector2 screen_pos)
    {
        return false;
    }

    public TTransform3 ViewportPos_ToWorldTransform(Vector2 screen_pos)
    {
        return default;
    }
}