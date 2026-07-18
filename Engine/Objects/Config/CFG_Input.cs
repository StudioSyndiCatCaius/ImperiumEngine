using ImperiumEngine.Classes;
using ImperiumEngine.Structs;
using Raylib_cs;
using System.Numerics;

namespace ImperiumEngine.Objects.Config;

public class CFG_Input : ImpConfig
{
    public TInputs inputs = new();

    public CFG_Input()
    {
        // Move — WASD
        var move = new TInputActionConfig();
        move.keys[TKey.Key(KeyboardKey.W)] = new TInputKeyConfig { axis = new Vector3( 0, 0, -1) };
        move.keys[TKey.Key(KeyboardKey.S)] = new TInputKeyConfig { axis = new Vector3( 0, 0,  1) };
        move.keys[TKey.Key(KeyboardKey.A)] = new TInputKeyConfig { axis = new Vector3(-1, 0,  0) };
        move.keys[TKey.Key(KeyboardKey.D)] = new TInputKeyConfig { axis = new Vector3( 1, 0,  0) };
        inputs.actions[new TLabel("move")] = move;

        // UI navigation — arrow keys
        var ui_nav = new TInputActionConfig();
        ui_nav.keys[TKey.Key(KeyboardKey.Up)]    = new TInputKeyConfig { axis = new Vector3( 0,  1, 0) };
        ui_nav.keys[TKey.Key(KeyboardKey.Down)]  = new TInputKeyConfig { axis = new Vector3( 0, -1, 0) };
        ui_nav.keys[TKey.Key(KeyboardKey.Left)]  = new TInputKeyConfig { axis = new Vector3(-1,  0, 0) };
        ui_nav.keys[TKey.Key(KeyboardKey.Right)] = new TInputKeyConfig { axis = new Vector3( 1,  0, 0) };
        inputs.actions[new TLabel("ui_nav")] = ui_nav;

        // UI confirm / cancel / page
        var ui_confirm = new TInputActionConfig();
        ui_confirm.keys[TKey.Key(KeyboardKey.Enter)] = new TInputKeyConfig();
        ui_confirm.keys[TKey.Key(KeyboardKey.Space)] = new TInputKeyConfig();
        inputs.actions[new TLabel("ui_confirm")] = ui_confirm;

        var ui_cancel = new TInputActionConfig();
        ui_cancel.keys[TKey.Key(KeyboardKey.Escape)] = new TInputKeyConfig();
        inputs.actions[new TLabel("ui_cancel")] = ui_cancel;

        var ui_page = new TInputActionConfig();
        ui_page.keys[TKey.Key(KeyboardKey.Q)] = new TInputKeyConfig { axis = new Vector3(-1, 0, 0) };
        ui_page.keys[TKey.Key(KeyboardKey.E)] = new TInputKeyConfig { axis = new Vector3( 1, 0, 0) };
        inputs.actions[new TLabel("ui_page")] = ui_page;
    }
}
