using System.Numerics;
using ImperiumCore.Structs;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Interfaces;

public interface I_InputTarget
{
    public void Input_OnBegin(TTag input, Vector3 axis) { }
    public void Input_OnEnd(TTag input, Vector3 axis) { }
    public void Input_OnUpdate(TTag input, double dt, Vector3 axis) { }
}