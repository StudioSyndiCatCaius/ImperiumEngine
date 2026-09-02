using System.Numerics;
using Engine.Core;
using Engine.Dialogs;
using Engine.Structs;

namespace Test;

public class TestComp : ImpComp
{
    public override void OnBegin()
    {
        base.OnBegin();
        ImpPlayer.Get().input_targets.Add(this);
        Console.WriteLine("TestComp started");
        DLG_Alert.Run("Testing Alert", () =>
        {
            Console.WriteLine("Alert closed");
        });
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
    }

    public override void Input_Pressed(ImpPlayer player, TLabel ia, Vector3 axis)
    {
        base.Input_Pressed(player, ia, axis);
        if (ia == "_cancel")
        {
            Console.WriteLine("Cancel pressed");
            
        }
    }
}