using Engine.Core;

namespace Engine.Comps._3D;

// Draws child Imp2D components as Ui elements relative to the Imp3D position
public class C3_UI : Imp3D
{
    [ImpVar] bool in_3d_space; //TRUE = draw elements on a 3d plane. FALSE = draw elements like normal (2d pass) but screen position relative to 3d position.

    public override void OnDraw3D(double dt, EDrawFlags flags = 0)
    {
        base.OnDraw3D(dt, flags);
        if (in_3d_space)
        {
            
        }
    }

    public override void OnDraw2D(double dt, EDrawFlags flags = 0)
    {
        base.OnDraw2D(dt, flags);
        if (!in_3d_space)
        {
            
        }
    }
}