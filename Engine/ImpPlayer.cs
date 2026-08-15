using System.Numerics;
using ImperiumEngine.Comps;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;


//Cursors can be mouse cursor OR a virtual cursor. every player has a cursor, but only player[0] can be mouse cursor.
public struct TCursorData
{
    public Vector2 position;
    public bool is_hidden;

    public ImpComp? target = null;
    
    public TCursorData()
    {
        position = default;
        is_hidden = false;
    }
}

public enum ECursorEvent
{
    Select_A, Select_B, Select_X, Select_Y, Scroll_Up, Scroll_Down,
}

public class ImpPlayer
{
    const float DEADZONE_MOVEMENT = 0.1f;
    
    // #################################################################################
    // Static
    // #################################################################################
    
    public bool cursor_can_hover_2d = true;
    public bool cursor_can_hover_3d = true;
    public float cursor_3d_trace_distance = 100f; // distance to trace cursor to check if hit/over and 3D ImpComps
    public ECollisionChannel cursor_3d_collision_channel = ECollisionChannel.Cursor;
    public Imp2D? target_focus = null; // ui focus target. changed when new ui element is clicked
    
    public static List<ImpPlayer> players=[ new ()]; // min player 1
    
    public static Dictionary<TLabel,TInputAction> input_actions=new();
    public static Dictionary<TLabel,TInputAction> native_actions=new()
    {
        // axis_scale maps a key onto an axis of the action's Vector3 result.
        // For movement that is X = forward/back, Z = right/left.
        ["_Move"] = new TInputAction()
        {
            keys =
            {
                [EInputKey.Key_W]=new TInputKey() { deadzone = DEADZONE_MOVEMENT, axis_scale = new Vector3(1,0,0)},
                [EInputKey.Key_A]=new TInputKey() { deadzone = DEADZONE_MOVEMENT, axis_scale = new Vector3(0,0,-1)},
                [EInputKey.Key_S]=new TInputKey() { deadzone = DEADZONE_MOVEMENT, axis_scale = new Vector3(-1,0,0)},
                [EInputKey.Key_D]=new TInputKey() { deadzone = DEADZONE_MOVEMENT, axis_scale = new Vector3(0,0,1)},
                [EInputKey.Pad_LeftStickX]=new TInputKey() { deadzone = DEADZONE_MOVEMENT, axis_scale = new Vector3(0,0,1)},
                [EInputKey.Pad_LeftStickY]=new TInputKey() { deadzone = DEADZONE_MOVEMENT, axis_scale = new Vector3(1,0,0)},
            }
        },
        // Looking is fed by raw pointer motion, so the axis carries pixels-per-frame:
        // X = pitch, Y = yaw. Consumers apply their own sensitivity.
        ["_Rotate"] = new TInputAction()
        {
            keys =
            {
                [EInputKey.Mouse_MoveX]=new TInputKey() { axis_scale = new Vector3(0,1,0)},
                [EInputKey.Mouse_MoveY]=new TInputKey() { axis_scale = new Vector3(1,0,0)},
            }
        },
        ["_Aim"] = new TInputAction() { keys = { [EInputKey.Mouse_Right]=new () } },
        ["_Jump"] = new TInputAction() { keys = { [EInputKey.Key_Space]=new () } },
        ["_Crouch"] = new TInputAction() { keys = { [EInputKey.Key_C]=new () } },
        ["_Sprint"] = new TInputAction() { keys = { [EInputKey.Key_LeftShift]=new () } },
        ["_DragDrop"] = new TInputAction() { keys = { [EInputKey.Mouse_Left]=new () } },
    }; //built-in input actions

    public static void Init()
    {
        void SetupCursorKey(ECursorEvent evnt, EInputKey key_km, EInputKey key_pad)
        {
            TInputAction ia = new();
            ia.keys.Add(key_km, new());
            ia.keys.Add(key_pad, new());
            native_actions.Add("Cursor_" + evnt, ia);
        }
        
        SetupCursorKey(ECursorEvent.Scroll_Down, EInputKey.Mouse_WheelDown, EInputKey.Pad_RightTrigger);
        SetupCursorKey(ECursorEvent.Scroll_Up, EInputKey.Mouse_WheelUp, EInputKey.Pad_LeftTrigger);
        SetupCursorKey(ECursorEvent.Select_A, EInputKey.Mouse_Left, EInputKey.Pad_FaceDown);
        SetupCursorKey(ECursorEvent.Select_B, EInputKey.Mouse_Right, EInputKey.Pad_FaceRight);
    }
    
    // ---------------------------------------
    // Input Actions
    // ---------------------------------------
    public static Dictionary<TLabel, TInputAction> InputActions_GetAll()
    {
        Dictionary<TLabel, TInputAction> _out = new(native_actions);

        foreach (var action in input_actions)
        {
            _out[action.Key] = action.Value;
        }

        return _out;
    }

    public static bool Action_IsDown(TLabel action, byte player = 0)
    {
        if(players.Count<=player) return false;
        return players[player].action_states.ContainsKey(action) && players[player].action_states[action] == EInputState.Down;
    }
    public static bool Action_IsPressed(TLabel action, byte player = 0)
    {
        if(players.Count<=player) return false;
        return players[player].action_states.ContainsKey(action) && players[player].action_states[action] == EInputState.Pressed;
    }
    public static bool Action_IsReleased(TLabel action, byte player = 0)
    {
        if(players.Count<=player) return false;
        return players[player].action_states.ContainsKey(action) && players[player].action_states[action] == EInputState.Released;
    }

    public static Vector3 Action_GetAxis(TLabel action, byte player = 0)
    {
        if(players.Count<=player) return Vector3.Zero;
        return players[player].action_axis.ContainsKey(action) ? players[player].action_axis[action] : Vector3.Zero;
    }
    
    
    // ---------------------------------------
    // Keys
    // ---------------------------------------
    public static EInputState Key_GetState(EInputKey key, byte player = 0)
    {
        if(players.Count<=player) return EInputState.None;
        return players[player].key_states.ContainsKey(key) ? players[player].key_states[key] : EInputState.None;
    }

    public static bool Key_IsDown(EInputKey key, byte player = 0)
    {
        if (!Key_Allowed(player)) return false;
        return players[player].key_states.ContainsKey(key) && players[player].key_states[key] == EInputState.Down;
    }
    public static bool Key_IsHeld(EInputKey key, byte player = 0)
    {
        if (!Key_Allowed(player)) return false;
        if (!players[player].key_states.TryGetValue(key, out EInputState s)) return false;
        return s is EInputState.Pressed or EInputState.Down;
    }
    public static bool Key_IsPressed(EInputKey key, byte player = 0)
    {
        if (!Key_Allowed(player)) return false;
        return players[player].key_states.ContainsKey(key) && players[player].key_states[key] == EInputState.Pressed;
    }

    public static bool Key_IsReleased(EInputKey key, byte player = 0)
    {
        if (!Key_Allowed(player)) return false;
        return players[player].key_states.ContainsKey(key) && players[player].key_states[key] == EInputState.Released;
    }

    // True when c still lives under the editor / app scene. Detached dialog
    // widgets and Destroyed comps fail this and must not keep hog / focus.
    public static bool Target_IsLive(ImpComp c)
    {
        if (c == null)
        {
            return false;
        }
        ImpComp root = ImpScene.current?.root;
        if (root == null)
        {
            return c.parent != null;
        }
        if (c == root)
        {
            return true;
        }
        return root.IsAncestorOf(c);
    }

    // Dialog / input_hog swallows Key_Is* for every comp outside that subtree.
    // Queries from outside Update (cursor phase, etc.) stay raw so hit-testing still works.
    public static bool Key_Allowed(byte player = 0)
    {
        if (players.Count <= player) return false;
        if (ImpComp.Updating == null) return true;
        ImpComp gate = C1_Dialog.Host ?? players[player].input_hog;
        if (gate == null) return true;
        for (ImpComp n = ImpComp.Updating; n != null; n = n.parent)
            if (n == gate) return true;
        return false;
    }
    
    
    public static bool KeyType_IsKeyboard(EInputKey k) => (int)k < 1000;
    public static bool KeyType_IsMouse   (EInputKey k) => (int)k is >= 1000 and < 2000;
    public static bool KeyType_IsGamepad (EInputKey k) => (int)k is >= 2000 and < 3000;
    public static bool KeyType_IsTouch   (EInputKey k) => (int)k is >= 3000 and < 4000;
    public static bool KeyType_IsStick   (EInputKey k) => (int)k >= 4000;
    
    // #################################################################################
    // Class
    // #################################################################################

    public int id;
    public TCursorData cursor;
    //a press only becomes a real drag once the cursor moves past grab_threshold, so a plain click never drops.
    public bool grab_is_active = false;
    public const float grab_threshold = 5f;
    private Vector2 grab_origin;
    
    private ImpComp? last_cursor_target = null;
    private Imp2D? last_focus_target = null;
    
    public ImpComp? target_cursor = null;
    public ImpComp? target_grabbed = null;
    public ImpComp? input_hog = null; //when valid, hogs all inputs, preventing input actions on any other comp until =null
    
    
    public Dictionary<EInputKey, EInputState> key_states=new ();
    public Dictionary<EInputKey, float> key_axis=new (); // scalar magnitude per key (1 for digital, delta for axes)
    public Dictionary<TLabel, EInputState> action_states=new ();
    public Dictionary<TLabel, Vector3> action_axis=new ();

    public EInputState InputAction_GetState(TLabel action)
    {
        return action_states.ContainsKey(action) ? action_states[action] : EInputState.None;
    }

    public Vector3 InputAction_GetAxis(TLabel action)
    {
        return action_axis.ContainsKey(action) ? action_axis[action] : Vector3.Zero;
    }
    
    public EInputState Key_GetState(EInputKey key) => key_states.ContainsKey(key) ? key_states[key] : EInputState.None;

    public Vector3 Key_GetAxis(EInputKey key)
    {
        return key_axis.TryGetValue(key, out float v) ? new Vector3(v) : Vector3.Zero;
    }
    
    public ImpPlayer()
    {
        
    }
    
    // ---------------------------------------
    // Cursor
    // ---------------------------------------
    public bool Cursor_IsInDimensions(TDimensions2 dim)
    {
        return dim.Contains(cursor.position);
    }

    public bool Cursor_Get3DPosition(out Vector3 pos, out Vector3 normal)
    {
        if (ImpApp.app == null)
        {
            pos = Vector3.Zero;
            normal = Vector3.Zero;
            return false;
        }
        Ray ray = Raylib.GetScreenToWorldRay(cursor.position, ImpApp.app.camera);
        pos = ray.Position;
        normal = ray.Direction;
        return true;
    }
    
    // ─────────────────────────────────────────────────────────────
    // Keyboard + Mouse  (only one allowed)
    // Call Update_Input, then scene Update (layout), then Update_Cursor.
    // ─────────────────────────────────────────────────────────────
    public void Update_Input(double dt)
    {
        // Dialogs reuse their panel by Detaching it before the overlay is Destroyed.
        // If hog / focus was a widget inside that panel, it is no longer under
        // ImpScene.current and would swallow Key_Is* / camera forever.
        if (input_hog != null && !Target_IsLive(input_hog))
        {
            input_hog = null;
        }
        if (target_focus != null && !Target_IsLive(target_focus))
        {
            target_focus = null;
        }

        // sync Mouse with Player 1 Cursor  ----------------------------------------------------
        if (id == 0)
        {
            cursor.position = Raylib.GetMousePosition();
            if (cursor.is_hidden != Raylib.IsCursorHidden())
            {
                if (cursor.is_hidden) Raylib.HideCursor();
                else Raylib.ShowCursor();
            }
            Update_Input_Single();
        }
        Update_Input_Multi(id);

        //Process Input Actions ----------------------------------------------------
        foreach (var ia in InputActions_GetAll())
        {
            List<Vector3> axis_list = new();
            bool _input_valid = false;
            foreach (var k in ia.Value.keys)
            {
                if (Key_GetState(k.Key) == EInputState.None) continue;

                Vector3 _axis = ia.Value.keys[k.Key].axis_scale;
                float _deadzone = ia.Value.keys[k.Key].deadzone;
                // Digital bindings (mouse buttons, etc.) leave axis_scale at zero —
                // they still count as active. Axis bindings need magnitude past deadzone.
                if (_axis.LengthSquared() <= 0f)
                {
                    _input_valid = true;
                    continue;
                }
                if (_axis.LengthSquared() > _deadzone * _deadzone)
                {
                    axis_list.Add(_axis * Key_GetAxis(k.Key));
                    _input_valid = true;
                }
            }

            EInputState current = InputAction_GetState(ia.Key);
            EInputState state_new = EInputState.None;

            if (_input_valid)
            {
                state_new = current is EInputState.None or EInputState.Released
                    ? EInputState.Pressed
                    : EInputState.Down;
            }
            else
            {
                state_new = current is EInputState.Pressed or EInputState.Down
                    ? EInputState.Released
                    : EInputState.None;
            }
            action_states[ia.Key] = state_new;
            action_axis[ia.Key] = ImpMath.V3_Average(axis_list);
        }

        //Process Input Hog ----------------------------------------------------
        if (input_hog != null)
        {
            input_hog.Update_Input(dt,this);
        }
        //Process Input Targets ----------------------------------------------------
        else
        {
            if (target_cursor != null) { target_cursor._Notify_AsCursorTarget( this,ENotifyGeneric.Update,dt); }
            if (target_focus != null) { target_focus._Notify_AsFocusTarget( this,ENotifyGeneric.Update,dt); }
            if (target_grabbed != null) { target_grabbed._Notify_AsGrabbedTarget( this,ENotifyGeneric.Update,dt); }
        }
    }

    public void Update_Cursor(double dt)
    {
        if (id != 0) return;

        target_cursor = null;
        
        if (cursor_can_hover_2d)
        {
            Imp2D? hit2d = Imp2D.Trace_Point(cursor.position, ImpScene.current.root);
            if (hit2d != null) target_cursor = hit2d;
        }

        if (target_cursor == null && cursor_can_hover_3d
            && Cursor_Get3DPosition(out Vector3 _cursor_3d_pos, out Vector3 _cursor_3d_normal))
        {
            TTraceResult3D result3D = Imp3D.Trace_Line(
                _cursor_3d_pos,
                _cursor_3d_pos + _cursor_3d_normal * cursor_3d_trace_distance,
                cursor_3d_collision_channel);
            if (result3D.hit && result3D.hit_comp != null)
                target_cursor = result3D.hit_comp;
        }

        if (input_hog == null
            && (Key_IsPressed(EInputKey.Mouse_Left)
                || Key_IsPressed(EInputKey.Mouse_Right)
                || Key_IsPressed(EInputKey.Mouse_Middle)))
        {
            target_focus = target_cursor as Imp2D;
        }

        if (last_focus_target != target_focus)
        {
            if (last_focus_target != null)
            {
                last_focus_target._Notify_AsFocusTarget(this, ENotifyGeneric.End, dt);
            }
            last_focus_target = target_focus;
            if (target_focus != null)
            {
                target_focus._Notify_AsFocusTarget(this, ENotifyGeneric.Begin, dt);
            }
        }

        void _GrabTargetEntry(ImpComp target, bool _state)
        {
            if (grab_is_active && target_grabbed != null && target != null)
            {
                target_grabbed._Notify_OnGrabDrop(this, _state ? ENotifyGrabTarget.Hover_AsTarget_Start : ENotifyGrabTarget.Hover_AsTarget_End, target, dt);
                target._Notify_OnGrabDrop(this, _state ? ENotifyGrabTarget.Hover_AsInstigator_Start : ENotifyGrabTarget.Hover_AsInstigator_End, target_grabbed, dt);
            }
        }

        if (last_cursor_target != target_cursor)
        {
            // Exit cursor over ------------
            if (last_cursor_target != null)
            {
                last_cursor_target._Notify_AsCursorTarget(this,ENotifyGeneric.End,dt);
                _GrabTargetEntry(last_cursor_target, false);
            };
            last_cursor_target = target_cursor;
            // enter cursor over -------------------
            if (target_cursor != null)
            {
                target_cursor._Notify_AsCursorTarget(this,ENotifyGeneric.Begin,dt);
                _GrabTargetEntry(target_cursor, true);
            }
        }

        foreach (var ia in action_states)
        {
            if (ia.Value != EInputState.Pressed || target_cursor == null) continue;
            string name = ia.Key.ToString();
            if (name.StartsWith("Cursor_")) name = name[7..];
            if (Enum.TryParse<ECursorEvent>(name, true, out ECursorEvent evnt))
            {
                target_cursor.Cursor_OnEvent(this, evnt);
            }
        }
        
        //grab check ---------------
        
        EInputState grab_state = InputAction_GetState("Cursor_" + ECursorEvent.Select_A);

        switch (grab_state)
        {
            //arm grab (_Notify_AsGrabbedTarget Begin waits for the drag threshold)
            case EInputState.Pressed:

                if (target_cursor != null && target_cursor.CursorGrab_IsEnabled(this) && target_grabbed == null)
                {
                    target_grabbed = target_cursor;
                    grab_is_active = false;
                    grab_origin = cursor.position;
                }

                break;

            //attempt drop
            case EInputState.Released:

                if (target_grabbed != null && grab_is_active)
                {
                    target_grabbed._Notify_OnGrabDrop(this, ENotifyGrabTarget.Drop_AsTarget, target_cursor, dt);
                    if (target_cursor != null)
                        target_cursor._Notify_OnGrabDrop(this, ENotifyGrabTarget.Drop_AsInstigator, target_grabbed, dt);
                    target_grabbed._Notify_AsGrabbedTarget(this, ENotifyGeneric.End, dt);
                }
                target_grabbed = null;
                grab_is_active = false;

                break;

            //update grab
            case EInputState.Down:
                if (target_grabbed == null) break;
                if (!grab_is_active)
                {
                    if (Vector2.Distance(cursor.position, grab_origin) < grab_threshold) break;
                    grab_is_active = true;
                    
                    target_grabbed._Notify_AsGrabbedTarget(this,ENotifyGeneric.Begin,dt);
                }
                break;
        }
        
    }

    public void Update_Player(double dt)
    {
        Update_Input(dt);
        Update_Cursor(dt);
    }
    
    void _ProcessKey(EInputKey key, EInputState state, float axis = 1f)
    {
        key_states[key] = state;
        // digital keys keep axis=1 while held; continuous axes pass raw delta
        key_axis[key] = state is EInputState.None or EInputState.Released ? 0f : axis;
    }
    
    private void Update_Input_Single()
    {
        Vector2 mouseDelta = Raylib.GetMouseDelta();
        float wheel = Raylib.GetMouseWheelMove();

        foreach (EInputKey key in Enum.GetValues<EInputKey>())
        {
            if (key == EInputKey.None) continue;

            int v = (int)key;
            EInputState state = EInputState.None;
            float axis = 1f;

            // Keyboard
            if (v < 1000)
            {
                var k = (KeyboardKey)v;
                if (Raylib.IsKeyPressed(k))       state = EInputState.Pressed;
                else if (Raylib.IsKeyDown(k))     state = EInputState.Down;
                else if (Raylib.IsKeyReleased(k)) state = EInputState.Released;
            }
            // Mouse
            else if (v is >= 1000 and < 2000)
            {
                int btn = v - 1000;

                if (btn <= 6)
                {
                    var m = (MouseButton)btn;
                    if (Raylib.IsMouseButtonPressed(m))       state = EInputState.Pressed;
                    else if (Raylib.IsMouseButtonDown(m))     state = EInputState.Down;
                    else if (Raylib.IsMouseButtonReleased(m)) state = EInputState.Released;
                }
                else if (key == EInputKey.Mouse_WheelUp && wheel > 0)
                {
                    state = EInputState.Pressed;
                    axis = wheel;
                }
                else if (key == EInputKey.Mouse_WheelDown && wheel < 0)
                {
                    state = EInputState.Pressed;
                    axis = -wheel;
                }
                else if (key == EInputKey.Mouse_MoveX && mouseDelta.X != 0)
                {
                    // continuous pointer axis — Down while moving this frame
                    state = EInputState.Down;
                    axis = mouseDelta.X;
                }
                else if (key == EInputKey.Mouse_MoveY && mouseDelta.Y != 0)
                {
                    state = EInputState.Down;
                    axis = mouseDelta.Y;
                }
            }

            _ProcessKey(key, state, axis);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Multi-device (gamepads, future sticks, etc.)
    // device = 0 → first gamepad, 1 → second, …
    // ─────────────────────────────────────────────────────────────
    private void Update_Input_Multi(int device)
    {
        if (device < 0 || device >= 4) return;
        if (!Raylib.IsGamepadAvailable(device)) return;

        foreach (EInputKey key in Enum.GetValues<EInputKey>())
        {
            if (key == EInputKey.None) continue;

            int v = (int)key;
            if (v is < 2000 or >= 3000) continue;   // only gamepad range

            EInputState state = EInputState.None;
            float axis = 1f;

            // Analog sticks (artificial keys 2050–2053)
            if (key is EInputKey.Pad_LeftStickX or EInputKey.Pad_LeftStickY
                or EInputKey.Pad_RightStickX or EInputKey.Pad_RightStickY)
            {
                GamepadAxis ga = key switch
                {
                    EInputKey.Pad_LeftStickX  => GamepadAxis.LeftX,
                    EInputKey.Pad_LeftStickY  => GamepadAxis.LeftY,
                    EInputKey.Pad_RightStickX => GamepadAxis.RightX,
                    _                         => GamepadAxis.RightY,
                };

                float a = Raylib.GetGamepadAxisMovement(device, ga);
                if (MathF.Abs(a) > DEADZONE_MOVEMENT)
                {
                    state = EInputState.Down;
                    axis = a;
                }
            }
            // Analog triggers (prefer axis pressure; fall back to digital button)
            else if (key is EInputKey.Pad_LeftTrigger or EInputKey.Pad_RightTrigger)
            {
                GamepadAxis ga = key == EInputKey.Pad_LeftTrigger
                    ? GamepadAxis.LeftTrigger
                    : GamepadAxis.RightTrigger;
                var gb = (GamepadButton)(v - 2000);

                // Raylib reports triggers in roughly -1..1 (released → pressed).
                // Remap to 0..1 pressure so axis magnitude is usable as a scalar.
                float a = (Raylib.GetGamepadAxisMovement(device, ga) + 1f) * 0.5f;
                a = Math.Clamp(a, 0f, 1f);

                if (a > DEADZONE_MOVEMENT)
                {
                    state = EInputState.Down;
                    axis = a;
                }
                else if (Raylib.IsGamepadButtonPressed(device, gb))  state = EInputState.Pressed;
                else if (Raylib.IsGamepadButtonDown(device, gb))    state = EInputState.Down;
                else if (Raylib.IsGamepadButtonReleased(device, gb)) state = EInputState.Released;
            }
            // Digital face / d-pad / bumper / thumb / menu buttons
            else
            {
                var g = (GamepadButton)(v - 2000);

                if (Raylib.IsGamepadButtonPressed(device, g))       state = EInputState.Pressed;
                else if (Raylib.IsGamepadButtonDown(device, g))     state = EInputState.Down;
                else if (Raylib.IsGamepadButtonReleased(device, g)) state = EInputState.Released;
            }

            _ProcessKey(key, state, axis);
        }
    }
}