using System.Numerics;
using Engine.Core;
using Engine.Interfaces;
using Engine.Enums;
using Engine.Globals;

namespace Engine.Structs;

public class TInputKey
{
    [ImpVar] public List<EInputKey> prereq_keys = new(); // keys that must be Down before this main key can be considered pressed
    [ImpVar] public float deadzone;
    // (0,0,0) = digital button (Space/Enter/etc). Non-zero = analog contribution for GetAxis.
    [ImpVar] public Vector3 axis_scale;

    public bool IsDigital => axis_scale.LengthSquared() <= 0f;

    public bool PrereqsHeld(ImpPlayer player)
    {
        foreach (var key in prereq_keys)
        {
            EInputState s = player.Key_GetState(key);
            if (s is not (EInputState.Pressed or EInputState.Down)) return false;
        }
        return true;
    }

    public bool IsActive(ImpPlayer player, Vector3 axis)
    {
        if (!PrereqsHeld(player)) return false;
        if (IsDigital) return true;
        return axis.Length() >= deadzone;
    }
    
}


public class TInputAction : I_Property
{
    [ImpVar] public string name;
    [ImpVar] public bool average_axis; //TRUE = average axis values of all keys that are Down | FALSE = add axis values of all keys that are Down
    [ImpVar] public Dictionary<EInputKey, TInputKey> keys = new();
    
    public EInputState GetState(ImpPlayer player)
    {
        EInputState last_state = EInputState.None;
        foreach (var pair in keys)
        {
            EInputState s = player.Key_GetState(pair.Key);
            if(s==EInputState.Down) return EInputState.Down; //if any key is Down, action is Down
            if(s==EInputState.Released) last_state=EInputState.Released;
            if(s==EInputState.Pressed) last_state=EInputState.Pressed;
        }
        return last_state;
    }

    //If at least one attached key is active, return true
    public bool IsActive(ImpPlayer player)
    {
        foreach (var pair in keys)
        {
            if(pair.Value.IsActive(player, Vector3.Zero)) return true;
        }
        return false;
    }
    
    public Vector3 GetAxis(ImpPlayer player)
    {
        Vector3 result = Vector3.Zero;
        Vector3 axis_sum = Vector3.Zero;
        int axis_count = 0;
        foreach (var pair in keys)
        {
            Vector3 _axis = player.Key_GetAxis(pair.Key)*pair.Value.axis_scale;
            if (pair.Value.IsActive(player, _axis))
            {
                axis_sum += _axis;
                axis_count++;
            }
        }

        if (axis_count > 0)
        {
            result = average_axis ? axis_sum / axis_count : axis_sum;
        }

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
            bool active = _ia.Value.IsActive(player);
            if (!active) continue;
            
            Vector3 _axis = _ia.Value.GetAxis(player);
            EInputState _state = _ia.Value.GetState(player);
            player.input_action_states[_ia.Key] = _state;

            List<ImpComp> targets = player.InputTarget_GetAll();
            for (int i = 0; i < targets.Count; i++)
                targets[i]._Input_Notif_Action(player, _ia.Key, _state, _axis, dt);
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
                    [EInputKey.Key_W]=new(){axis_scale=GMath.WORLD_FORWARD},
                    [EInputKey.Key_A]=new(){axis_scale=GMath.WORLD_LEFT},
                    [EInputKey.Key_S]=new(){axis_scale=-GMath.WORLD_FORWARD},
                    [EInputKey.Key_D]=new(){axis_scale=-GMath.WORLD_LEFT},
                    
                    [EInputKey.Pad_LeftStickX]=new(){axis_scale=GMath.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Pad_LeftStickY]=new(){axis_scale=GMath.WORLD_LEFT,deadzone = DEADZONE}
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
                    [EInputKey.Key_Up]=new(){axis_scale=GMath.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Key_Down]=new(){axis_scale=-GMath.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Key_Left]=new(){axis_scale=GMath.WORLD_LEFT,deadzone = DEADZONE},
                    [EInputKey.Key_Right]=new(){axis_scale=-GMath.WORLD_LEFT,deadzone = DEADZONE},
                    
                    [EInputKey.Pad_DPadUp]=new(){axis_scale=GMath.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Pad_DPadDown]=new(){axis_scale=-GMath.WORLD_FORWARD,deadzone = DEADZONE},
                    [EInputKey.Pad_DPadLeft]=new(){axis_scale=GMath.WORLD_LEFT,deadzone = DEADZONE},
                    [EInputKey.Pad_DPadRight]=new(){axis_scale=-GMath.WORLD_LEFT,deadzone = DEADZONE},
                }
            },
            
            ["_confirm"]=new() { keys = { [EInputKey.Key_Enter]=new(){}, [EInputKey.Pad_FaceDown]=new() {}, } },
            ["_cancel"]=new() { keys = { [EInputKey.Key_Escape]=new(){}, [EInputKey.Pad_FaceRight]=new(){}, } },
        }
    };
}