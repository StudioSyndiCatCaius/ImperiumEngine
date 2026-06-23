using ImperiumCore.Structs;

namespace ImperiumCore.Classes;

using System.Numerics;
using Silk.NET.Input;

// A single player: an input endpoint plus the UI focus it owns. ImpApp keeps a
// list of these and guarantees at least one always exists. Devices are polled
public class ImpPlayer
{
    public IInputContext InputContext;
    public ImpComponent2D FocusedWidget;

    private List<ImpDevice> Devices; 
    
    // -------------------------------------------------------------------------
    // Focus
    // -------------------------------------------------------------------------
    
    public bool Focus_Set(ImpComponent2D? widget)
    {

        return true;
    }

    public void Focus_Clear()
    {
        Focus_Set(null);
    }


  

    // -------------------------------------------------------------------------
    // Single-key queries
    // -------------------------------------------------------------------------

    public bool Key_IsDown(TKey key)
    {
        return false;
    }

    public bool Key_JustPressed(TKey key)
    {
        return false;
    }

    public Vector3 Key_GetAxis(TKey key)
    {

        return Key_IsDown(key) ? Vector3.UnitX : Vector3.Zero;
    }

    // -------------------------------------------------------------------------
    // Action (TInput) queries
    // -------------------------------------------------------------------------

    public bool Input_IsDown(TInput input)
    {

        return false;
    }

    public bool Input_JustPressed(TInput input)
    {

        return false;
    }

    public Vector3 Input_GetAxis(TInput input)
    {
        var result = Vector3.Zero;
        
        return result;
    }


    
}
