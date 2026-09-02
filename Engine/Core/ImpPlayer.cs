using System.Numerics;
using Engine.Assets;
using Engine.Enums;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Core;

public struct TCursorData
{
    public Vector2 position;
    public bool is_visible=true;

    public TCursorData()
    {
        position = default;
    }
}

public class ImpPlayer
{
    const EInputKey KEY_DRAG_START_KM=EInputKey.Mouse_Left;
    const EInputKey KEY_DRAG_START_PAD=EInputKey.Pad_FaceDown;
    // ==============================================================================================================
    // STATIC
    // ==============================================================================================================
    [ImpVar][Config] public static TInputSet input_actions = new();

    
    public static ImpPlayer Get(int id = 0)
    {
        return App.players[id];
    }
    
    // -------------------------------------------------------------
    // Key
    // -------------------------------------------------------------
    public static bool KeyType_IsKeyboard(EInputKey k) => (int)k < 1000;
    public static bool KeyType_IsMouse   (EInputKey k) => (int)k is >= 1000 and < 2000;
    public static bool KeyType_IsGamepad (EInputKey k) => (int)k is >= 2000 and < 3000;
    public static bool KeyType_IsTouch   (EInputKey k) => (int)k is >= 3000 and < 4000;
    public static bool KeyType_IsStick   (EInputKey k) => (int)k >= 4000;
    
    // ==============================================================================================================
    // Class
    // ==============================================================================================================
    public int id;
    public Imp3D pawn;
    public Vector3 control_rotation = Vector3.Zero;
    public TCursorData cursor_data = new();

    // -------------------------------------------------------------
    // INPUT
    // -------------------------------------------------------------
    public Dictionary<EInputKey, EInputState> input_key_states = new();
    public Dictionary<TLabel, EInputState> input_action_states = new();
    public List<ImpComp> input_targets=new ();

    public EInputState Key_GetState(EInputKey key)
    {
        if( !input_key_states.ContainsKey(key)) input_key_states[key] = EInputState.None;
        return input_key_states[key];
    }

    //this is NOT a check on the keys, EInputState. It checks raylib to sey if this key input is down/active (includes gamepad stick & mouse axis
    public bool Key_IsDown(EInputKey key)
    {
        return Key_GetAxis(key).LengthSquared() > 0f;
    }

    public bool Key_IsDragStart(EInputKey k)
    {
        if (k == KEY_DRAG_START_KM) return true;
        return cursor_data.is_visible && k == KEY_DRAG_START_PAD;
    }

    bool Key_IsPointer(EInputKey k) =>
        KeyType_IsMouse(k) || KeyType_IsTouch(k) || Key_IsDragStart(k);

    public Vector3 Key_GetAxis(EInputKey key)
    {
        int keyId = (int)key;

        // Mouse + keyboard are only controlled by player 0.
        if (KeyType_IsKeyboard(key))
        {
            if (id != 0 || key == EInputKey.None)
                return Vector3.Zero;

            return Raylib.IsKeyDown((KeyboardKey)keyId) ? Vector3.One : Vector3.Zero;
        }

        if (KeyType_IsMouse(key))
        {
            if (id != 0)
                return Vector3.Zero;

            return key switch
            {
                EInputKey.Mouse_Left => Raylib.IsMouseButtonDown(MouseButton.Left) ? Vector3.One : Vector3.Zero,
                EInputKey.Mouse_Right => Raylib.IsMouseButtonDown(MouseButton.Right) ? Vector3.One : Vector3.Zero,
                EInputKey.Mouse_Middle => Raylib.IsMouseButtonDown(MouseButton.Middle) ? Vector3.One : Vector3.Zero,
                EInputKey.Mouse_Side1 => Raylib.IsMouseButtonDown(MouseButton.Back) ? Vector3.One : Vector3.Zero,
                EInputKey.Mouse_Side2 => Raylib.IsMouseButtonDown(MouseButton.Forward) ? Vector3.One : Vector3.Zero,

                EInputKey.Mouse_WheelUp => Raylib.GetMouseWheelMove() > 0f
                    ? new Vector3(Raylib.GetMouseWheelMove(), 0f, 0f)
                    : Vector3.Zero,
                EInputKey.Mouse_WheelDown => Raylib.GetMouseWheelMove() < 0f
                    ? new Vector3(-Raylib.GetMouseWheelMove(), 0f, 0f)
                    : Vector3.Zero,

                EInputKey.Mouse_MoveX => new Vector3(Raylib.GetMouseDelta().X, 0f, 0f),
                EInputKey.Mouse_MoveY => new Vector3(Raylib.GetMouseDelta().Y, 0f, 0f),

                _ => Vector3.Zero
            };
        }

        // Player 1 -> gamepad 0, player 2 -> gamepad 1, etc.
        int deviceId = id - 1;

        if (KeyType_IsGamepad(key))
        {
            if (deviceId < 0 || !Raylib.IsGamepadAvailable(deviceId))
                return Vector3.Zero;

            return key switch
            {
                EInputKey.Pad_LeftStickX => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.LeftX), 0f,
                    0f),
                EInputKey.Pad_LeftStickY => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.LeftY), 0f,
                    0f),
                EInputKey.Pad_RightStickX => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.RightX),
                    0f, 0f),
                EInputKey.Pad_RightStickY => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.RightY),
                    0f, 0f),

                EInputKey.Pad_LeftTrigger => new Vector3(
                    MathF.Max(0f, Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.LeftTrigger)), 0f, 0f),
                EInputKey.Pad_RightTrigger => new Vector3(
                    MathF.Max(0f, Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.RightTrigger)), 0f, 0f),

                _ => Raylib.IsGamepadButtonDown(deviceId, (GamepadButton)(keyId - 2000)) ? Vector3.One : Vector3.Zero
            };
        }

        if (KeyType_IsTouch(key))
        {
            int touchIndex = keyId - 3000;
            return touchIndex >= 0 && touchIndex < Raylib.GetTouchPointCount()
                ? Vector3.One
                : Vector3.Zero;
        }

        // Generic joystick fallback: raylib exposes these through gamepad APIs only.
        // Treat Stick_* as aliases for the current player's gamepad where possible.
        if (KeyType_IsStick(key))
        {
            if (deviceId < 0 || !Raylib.IsGamepadAvailable(deviceId))
                return Vector3.Zero;

            if (keyId is >= 4000 and <= 4015)
                return Raylib.IsGamepadButtonDown(deviceId, (GamepadButton)(keyId - 4000)) ? Vector3.One : Vector3.Zero;

            return key switch
            {
                EInputKey.Stick_AxisX => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.LeftX), 0f,
                    0f),
                EInputKey.Stick_AxisY => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.LeftY), 0f,
                    0f),
                EInputKey.Stick_AxisRx => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.RightX), 0f,
                    0f),
                EInputKey.Stick_AxisRy => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.RightY), 0f,
                    0f),
                EInputKey.Stick_AxisZ => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.LeftTrigger),
                    0f, 0f),
                EInputKey.Stick_AxisRz => new Vector3(Raylib.GetGamepadAxisMovement(deviceId, GamepadAxis.RightTrigger),
                    0f, 0f),

                EInputKey.Stick_HatUp => Raylib.IsGamepadButtonDown(deviceId, GamepadButton.LeftFaceUp)
                    ? Vector3.One
                    : Vector3.Zero,
                EInputKey.Stick_HatDown => Raylib.IsGamepadButtonDown(deviceId, GamepadButton.LeftFaceDown)
                    ? Vector3.One
                    : Vector3.Zero,
                EInputKey.Stick_HatLeft => Raylib.IsGamepadButtonDown(deviceId, GamepadButton.LeftFaceLeft)
                    ? Vector3.One
                    : Vector3.Zero,
                EInputKey.Stick_HatRight => Raylib.IsGamepadButtonDown(deviceId, GamepadButton.LeftFaceRight)
                    ? Vector3.One
                    : Vector3.Zero,

                _ => Vector3.Zero
            };
        }

        return Vector3.Zero;
    }
    
    // -------------------------------------------------------------
    // ???
    // -------------------------------------------------------------
    public Imp2D target_2d; //2d focus target
    public Imp3D target_3d; //3d controlled pawn
    public ImpComp drag_target; //object being dragged
    private ImpComp _drag_target_prev;
    private ImpComp _press_target;
    
    // -------------------------------------------------------------
    // Cursor
    // -------------------------------------------------------------
    private ImpComp _cursor_target_prev;
    public ImpComp cursor_target; //component that cursor is hovering over
    private bool _cursor_was_vis;
    private Imp2D _focus_target_prev;
    
    public Imp2D focus_target;
    public ECollisionChannel cursor_collision_channel;

    A_Scene _cursor_scene;
    Vector2 _cursor_pos_override;
    bool _cursor_redirect;

    // Map a 2D cursor into a scene's bounds space (editor viewports, offscreen targets).
    public void Cursor_Redirect(A_Scene scene, Vector2 pos_2d)
    {
        _cursor_redirect = true;
        _cursor_scene = scene;
        _cursor_pos_override = pos_2d;
    }
    

    public Vector3 Cursor_3DPosition()
    {
        return default;
    }
    
    public Vector3 Cursor_3DNormal()
    {
        return default;
    }
    
    public void Update(double dt)
    {
        // ---------- CURSOR
        if (id == 0)
        {
            if (_cursor_redirect)
            {
                cursor_data.position = _cursor_pos_override;
            }
            else
            {
                // Raylib HighDPI draws in screen pixels (screenScale). Do not multiply by render/screen.
                cursor_data.position = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), App.camera_2d);
            }
            if (_cursor_was_vis != cursor_data.is_visible)
            {
                _cursor_was_vis = cursor_data.is_visible;
                if(cursor_data.is_visible) Raylib.ShowCursor();
                else Raylib.HideCursor();
            }
        }
        else if (cursor_data.is_visible)
        {
            Vector2 stick = new(Key_GetAxis(EInputKey.Pad_LeftStickX).X, Key_GetAxis(EInputKey.Pad_LeftStickY).X);
            cursor_data.position += stick * 800f * (float)dt;
            TBounds2 vp = App.viewport_main.Bounds;
            cursor_data.position = Vector2.Clamp(cursor_data.position, vp.start, vp.end);
        }
        
        //do cursor trace
        cursor_target = _cursor_redirect
            ? Imp.Trace2D_ForComp(cursor_data.position, _cursor_scene?.root)
            : Imp.Trace2D_ForComp(cursor_data.position);
        _cursor_redirect = false;
        _cursor_scene = null;
        if (cursor_target == null)
        {
            Vector3 _start=Cursor_3DPosition();
            Vector3 _end = default; //trace out from cursor screen position
            TTraceResult3D _res= Imp.Trace3D_Line(_start, _end, cursor_collision_channel);
            if (_res.hit_comp != null) cursor_target = _res.hit_comp;
        }
        
        if (cursor_target != _cursor_target_prev)
        {
            if(_cursor_target_prev!=null) _cursor_target_prev._NotifyAs_CursorTarget(this,ENotifyGeneric.End,dt);
            _cursor_target_prev = cursor_target;
            if(cursor_target!=null) cursor_target._NotifyAs_CursorTarget(this,ENotifyGeneric.Begin,dt);
        }
        else if( cursor_target != null)
        {
            cursor_target._NotifyAs_CursorTarget(this,ENotifyGeneric.Update,dt);
        }
        
        // ---------- KEYS
        // This method is probably SLOW, consider replacing later
        EInputState _drag_key_state = EInputState.None;
        foreach (var _k in Enum.GetValues<EInputKey>())
        {
            EInputState _state_old=Key_GetState(_k);
            EInputState _state_new = EInputState.None;
            bool _is_down=Key_IsDown(_k);
            
            switch (_state_old)
            {
                case EInputState.None:
                    _state_new = _is_down ? EInputState.Pressed : EInputState.None;
                    break;
                case EInputState.Down:
                    _state_new = _is_down ? EInputState.Down : EInputState.Released;
                    break;
                case EInputState.Pressed:
                    _state_new = _is_down ? EInputState.Down : EInputState.Released;
                    break;
                case EInputState.Released:
                    _state_new = _is_down ? EInputState.Pressed : EInputState.None;
                    break;
            }

            input_key_states[_k] = _state_new;
            if (Key_IsDragStart(_k))
            {
                if (_state_new == EInputState.Pressed) _drag_key_state = EInputState.Pressed;
                else if (_state_new == EInputState.Released && _drag_key_state != EInputState.Pressed)
                    _drag_key_state = EInputState.Released;
            }
        }
        
        if (_drag_key_state == EInputState.Pressed && cursor_target != null)
        {
            _press_target = cursor_target;
            if (cursor_target.Dragging_IsAllowed())
            {
                drag_target = cursor_target;
                focus_target = null; //you cannot have a focus target while dragging a comp
            }
            else if (cursor_target is Imp2D ui)
            {
                focus_target = ui;
            }
        }
        
        // ---------- DRAG & DROP
        bool _drag_released = _drag_key_state == EInputState.Released;
        if (drag_target != _drag_target_prev)
        {
            if( _drag_target_prev != null) _drag_target_prev._NotifyAs_DragTarget( this, cursor_target, ENotifyGeneric.End, dt);
            _drag_target_prev = drag_target;
            if(drag_target != null) drag_target._NotifyAs_DragTarget( this, null, ENotifyGeneric.Begin, dt);
        }
        else if( drag_target != null && !_drag_released)
        {
            drag_target._NotifyAs_DragTarget( this, cursor_target, ENotifyGeneric.Update, dt);
        }
        
        if (drag_target != null)
        {
            Imp2D preview = drag_target.Dragging_GetPreview();
            if (preview != null) preview.Position_Set(cursor_data.position, true);
        }
        
        // ---------- FOCUS TARGET
        if (focus_target != _focus_target_prev)
        {
            if(_focus_target_prev!=null) _focus_target_prev._NotifyAs_FocusTarget(this,ENotifyGeneric.End,dt);
            _focus_target_prev = focus_target;
            if(focus_target!=null) focus_target._NotifyAs_FocusTarget(this,ENotifyGeneric.Begin,dt);
        }
        else if( focus_target != null)
        {
            focus_target._NotifyAs_FocusTarget(this,ENotifyGeneric.Update,dt);
        }
        
        // ---------- DISPATCH
        ImpComp _pointer_target = _press_target ?? cursor_target;
        foreach (var _pair in input_key_states)
        {
            if (_pair.Value == EInputState.None) continue;
            EInputKey _k = _pair.Key;
            EInputState _state_new = _pair.Value;
            
            if (drag_target != null)
            {
                drag_target._InputAs_DragTarget(this, _k, _state_new, dt);
            }
            else if (Key_IsPointer(_k))
            {
                if (_pointer_target != null) _pointer_target._InputAs_CursorTarget(this, _k, _state_new, dt);
            }
            else if (focus_target != null)
            {
                focus_target._InputAs_FocusTarget(this, _k, _state_new, dt);
            }
        }
        
        if (_drag_released)
        {
            if (drag_target != null)
            {
                drag_target._NotifyAs_DragTarget(this, cursor_target, ENotifyGeneric.End, dt);
                _drag_target_prev = null;
                drag_target = null;
            }
            _press_target = null;
        }
        
        // ---------- INPUT ACTIONS
        TInputSet.BUILTINS.Update(this, dt);
        input_actions.Update(this, dt);
    }
}