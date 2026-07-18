using System.Numerics;
using ImperiumEngine.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Interfaces;

public interface I_InputTarget
{

    virtual void Input_Begin(ImpPlayer player, TLabel action, Vector3 axis)
    {
        
    }
    
    virtual void Input_Update(ImpPlayer player,TLabel action, Vector3 axis, float delta)
    {
        
    }
    
    virtual void Input_End(ImpPlayer player,TLabel action, Vector3 axis)
    {
        
    }
}