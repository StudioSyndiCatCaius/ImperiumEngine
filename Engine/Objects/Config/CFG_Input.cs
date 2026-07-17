using ImperiumEngine.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects.Config;

public class CFG_Input : ImpConfig
{
    [Exposed] public Dictionary<TLabel, TInputActionConfig> input_actions;
    
    public CFG_Input()
    {
        input_actions.Add(new TLabel("move"), new TInputActionConfig()); // WASD
        input_actions.Add(new TLabel("rotate"), new TInputActionConfig()); // Mouse
        
        input_actions.Add(new TLabel("ui_nav"), new TInputActionConfig()); // Up, Down, Left, Right
        input_actions.Add(new TLabel("ui_confirm"), new TInputActionConfig()); // Enter, Space
        input_actions.Add(new TLabel("ui_cancel"), new TInputActionConfig()); // Escape
        input_actions.Add(new TLabel("ui_page"), new TInputActionConfig()); // Q=-1 | E=1
    }
}