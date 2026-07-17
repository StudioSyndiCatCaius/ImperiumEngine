using System.Numerics;
using ImperiumCore.Structs;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;

namespace ImperiumCore.Classes.Components;


//player singleton game state
public class ImpGameState : ImpComponent, I_InputTarget
{
    public static async Task<ImpGameState> Run()
    {
        return null;
    }

    public void State_Shutdown(string reason)
    {
        //shutstate down
        OnState_Shutdown.Invoke(this,reason);
        Component_Destroy();
    }
    
    public Action<ImpGameState,string> OnState_Notify;
    public Action<ImpGameState,string> OnState_Shutdown;
    
    
    [ImpVar] [Exposed] public TTagSet StateTags; //tags that identify this state
    [ImpVar] [Exposed] public TTagSet BlockedStates; //GameStates that must not be active for this state to be active
    [ImpVar] [Exposed] public TTagSet RequiredStates; //GameStates that must be active for this state to be active
  
}