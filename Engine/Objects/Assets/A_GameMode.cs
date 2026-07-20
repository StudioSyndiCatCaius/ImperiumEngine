using ImperiumEngine.Classes;
using ImperiumEngine.Objects._1D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects.Assets;

public class A_GameMode : ImpAsset
{
    // ----------------------------------------------------------------------------------------
    // Game States
    // ----------------------------------------------------------------------------------------
    
    //states activated immediately on game mode start
    [ImpVar] public List<TRef<C1_GameState>> states_PreLoad = new();

    //states that will be run in order on start
    [ImpVar] public List<TRef<C1_GameState>> states_Load = new();

    //states activated immediately when all load states have finished
    [ImpVar] public List<TRef<C1_GameState>> states_PostLoad = new();

    //states activated immediately when all load states have finished
    [ImpVar] public List<TRef<C1_GameState>> states_automatic = new();
    
    //frequency (in seconds) automatic GameStates will attempt to be activated (if not already)
    [ImpVar] public double AutomaticStateFrequency=0.2;

    bool State_IsActive(TRef<C1_GameState> state)
    {
        return false;
    }
    
    // ----------------------------------------------------------------------------------------
    // Player
    // ----------------------------------------------------------------------------------------
    [ImpVar] public TRef<ImpComponent> player_default_pawn;
    
    [ImpVar] public bool use_custom_camera;
    [ImpVar] public A_CameraConfig custom_camera_config;
}