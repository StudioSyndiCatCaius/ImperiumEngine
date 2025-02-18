using System.Numerics;

namespace ImperiumEngine.Source.Objects._3D;

using Raylib_cs;

public class O3D_TestObject : ImpComponent3D
{
    public override void On_Draw3D(double delta)
    {
        Console.WriteLine("drawing 3d cube {0}", Location_Get());
       Raylib.DrawCube(Location_Get(),1,1,1,Color.White);
        base.On_Draw3D(delta);
    }
}