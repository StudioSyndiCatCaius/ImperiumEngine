using System.Numerics;
using Engine.Core;
using Engine.Interfaces;
using Engine.Enums;

namespace Engine.Structs;

public class TInputKey
{
    [ImpVar] public List<EInputKey> prereq_keys = new(); // keys that must be Down before this main key can be considered pressed
    [ImpVar] public float deadzone;
    [ImpVar] public Vector3 axis_scale;

    public bool IsDown(ImpPlayer player, Vector3 axis)
    {
        foreach (var key in prereq_keys)
        {
            if(player.Key_GetState(key) != EInputState.Down) return false;
        }
        if(axis.Length() < deadzone) return false;
        return true;
    }
    
}


public class TInputAction : I_Property
{
    [ImpVar] public string name;
    [ImpVar] public bool average_axis; //TRUE = average axis values of all keys that are Down | FALSE = add axis values of all keys that are Down
    [ImpVar] public Dictionary<EInputKey, TInputKey> keys = new();

    public EInputState GetState(ImpPlayer player)
    {
        EInputState result = EInputState.None;

        foreach (var key in keys.Keys)
        {
            EInputState state = player.Key_GetState(key);

            if (state == EInputState.Pressed)
                return EInputState.Pressed;

            if (state == EInputState.Released)
                result = EInputState.Released;
            else if (state == EInputState.Down && result == EInputState.None)
                result = EInputState.Down;
        }

        return result;
    }

    public Vector3 GetAxis(ImpPlayer player)
    {
        Vector3 result = Vector3.Zero;
        int downCount = 0;

        foreach (var pair in keys)
        {
            EInputKey key = pair.Key;
            TInputKey inputKey = pair.Value;

            EInputState state = player.Key_GetState(key);
            if (state is not (EInputState.Pressed or EInputState.Down))
                continue;

            Vector3 rawAxis = player.Key_GetAxis(key);
            Vector3 scaledAxis = rawAxis * inputKey.axis_scale;

            if (!inputKey.IsDown(player, scaledAxis))
                continue;

            result += scaledAxis;
            downCount++;
        }

        if (average_axis && downCount > 0)
            result /= downCount;

        return result;
    }
}


public class TInputSet
{
    [ImpVar] public Dictionary<TLabel, TInputAction> actions = new();
    
    public void Update(ImpPlayer player, double dt)
    {
        foreach (var _ia in actions)
        {
            foreach (var _targ in player.input_targets)
            {
                EInputState _state = _ia.Value.GetState(player);
                Vector3 _axis = _ia.Value.GetAxis(player);
                switch (_state)
                {
                    case EInputState.Down:
                        _targ.Input_Down(player,_ia.Key,_axis,dt);
                        _targ.script_instance?.Call("Input_Down", player, _ia.Key, _axis, dt);
                        break;
                    case EInputState.Pressed:
                        _targ.Input_Pressed(player,_ia.Key,_axis);
                        _targ.script_instance?.Call("Input_Pressed", player, _ia.Key, _axis);
                        break;
                    case EInputState.Released:
                        _targ.Input_Released(player,_ia.Key,_axis);
                        _targ.script_instance?.Call("Input_Released", player, _ia.Key, _axis);
                        break;
                }
            }
        }
    }
    
    // -----------------------------------------------------------------
    // Built In Actions
    // -----------------------------------------------------------------
    static readonly float DEADZONE = 0.1f;
    
    public static TInputSet BUILTINS = new()
    {
        actions =new()
        {
            // ---- MOVE
            ["_move"]=new()
            {
                keys =new()
                {
                    [EInputKey.Key_W]=new(){axis_scale=Imp.WORLD_FORWARD},
                    [EInputKey.Key_A]=new(){axis_scale=Imp.WORLD_LEFT},
                    [EInputKey.Key_S]=new(){axis_scale=-Imp.WORLD_FORWARD},
                    [EInputKey.Key_D]=new(){axis_scale=-Imp.WORLD_LEFT},
                    
                    [EInputKey.Pad_LeftStickX]=new(){axis_scale=Imp.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Pad_LeftStickY]=new(){axis_scale=Imp.WORLD_LEFT,deadzone = DEADZONE}
                },
            },
            // ---- ROTATE
            ["_rotate"]=new()
            {
                keys =new()
                {
                    [EInputKey.Mouse_MoveX]=new(){axis_scale=new Vector3(1,0,0),deadzone = DEADZONE},
                    [EInputKey.Mouse_MoveY]=new(){axis_scale=new Vector3(0,1,0),deadzone = DEADZONE},
                    
                    [EInputKey.Pad_RightStickX]=new(){axis_scale=new Vector3(1,0,0),deadzone = DEADZONE},
                    [EInputKey.Pad_RightStickY]=new(){axis_scale=new Vector3(0,1,0),deadzone = DEADZONE},
                }
            },
            // ---- ZOOM
            ["_scroll"]=new()
            {
                keys =new()
                {
                    [EInputKey.Mouse_WheelUp]=new(){axis_scale=new Vector3(1,0,0),deadzone = DEADZONE},
                    [EInputKey.Mouse_WheelDown]=new(){axis_scale=new Vector3(-1,0,0),deadzone = DEADZONE}
                }
            },
            
            ["_nav"]=new ()
            {
                keys =new()
                {
                    [EInputKey.Key_Up]=new(){axis_scale=Imp.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Key_Down]=new(){axis_scale=-Imp.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Key_Left]=new(){axis_scale=Imp.WORLD_LEFT,deadzone = DEADZONE},
                    [EInputKey.Key_Right]=new(){axis_scale=-Imp.WORLD_LEFT,deadzone = DEADZONE},
                    
                    [EInputKey.Pad_DPadUp]=new(){axis_scale=Imp.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Pad_DPadDown]=new(){axis_scale=-Imp.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Pad_DPadLeft]=new(){axis_scale=Imp.WORLD_LEFT,deadzone = DEADZONE},
                    [EInputKey.Pad_DPadRight]=new(){axis_scale=-Imp.WORLD_LEFT,deadzone = DEADZONE},
                }
            },
            
            ["_confirm"]=new() { keys = { [EInputKey.Key_Enter]={}, [EInputKey.Pad_FaceDown]={}, } },
            ["_cancel"]=new() { keys = { [EInputKey.Key_Escape]={}, [EInputKey.Pad_FaceRight]={}, } },
        }
    };
}