using ImperiumEngine.Structs;

namespace ImperiumCore.Classes.Components;


//player singleton game state
public class ImpGameState : ImpComponent
{
    public static async Task<ImpGameState> Run()
    {
        return null;
    }
    
    [ImpVar] [Exposed] public TTagSet StateTags; //tags that identify this state
    [ImpVar] [Exposed] public TTagSet BlockedStates; //GameStates that must not be active for this state to be active
    [ImpVar] [Exposed] public TTagSet RequiredStates; //GameStates that must be active for this state to be active
}