using System.Numerics;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Interfaces;

public interface I_InputTarget 
{
    [ImpFunc] public virtual bool Input_IsEnabled()
    {
        return true;
    }
    
    [ImpFunc] public virtual void Input_OnPressed(TInputAction action, Vector3 axis)
    {
        
    }
    [ImpFunc] public virtual void Input_OnUpdate(TInputAction action, Vector3 axis, double dt)
    {
        
    }
    [ImpFunc] public virtual void Input_OnReleased(TInputAction action, Vector3 axis)
    {
        
    }
}