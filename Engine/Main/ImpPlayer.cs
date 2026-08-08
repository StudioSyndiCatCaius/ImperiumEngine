using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Main;



public class ImpPlayer
{
    const float DEADZONE_MOVEMENT = 0.1f;
    
    // =====================================================================================
    // Statics
    // =====================================================================================
    
    [ImpConfig] public static byte max_players=1;

    //input_actions and input_natives are both used to define the input bindings for the player
    [ImpConfig] public Dictionary<TLabel,TInputAction> input_actions = new Dictionary<TLabel,TInputAction>();

    // Engine-supplied bindings, shared by every player. A player's own input_actions
    // shadow these by name, so a project can rebind an action without losing the rest.
    public static readonly Dictionary<TLabel,TInputAction> input_natives = new()
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
        ["_Aim"] = new TInputAction() { keys = { [EInputKey.Mouse_Right]=new TInputKey() { }, } },
        ["_Jump"] = new TInputAction() { keys = { [EInputKey.Key_Space]=new TInputKey() { }, } },
        ["_Crouch"] = new TInputAction() { keys = { [EInputKey.Key_C]=new TInputKey() { }, } },
        ["_Sprint"] = new TInputAction() { keys = { [EInputKey.Key_LeftShift]=new TInputKey() { }, } },
        ["_DragDrop"] = new TInputAction() { keys = { [EInputKey.Mouse_Left]=new TInputKey() { }, } },
    };

    public static bool is_mouse_visible=true;

    public static List<ImpPlayer> players= new List<ImpPlayer>();

    static Action<bool>? on_PlayerConnected;

    public static ImpPlayer? Create()
    {
        if (players.Count < max_players)
        {
            ImpPlayer _new= new ImpPlayer();
            _new.player_id = (byte)players.Count;
            players.Add(_new);
            on_PlayerConnected?.Invoke(true);
            return _new;
        }

        return null;
    }

    public static bool RemoveIndex(int index)
    {
        if (players.Count < index)
        {
            players.RemoveAt(index);
            on_PlayerConnected?.Invoke(false);
            return true;
        }
        return false;
    }
    
    // -----------------------------------------------
    // Grab (Drag&Drop)
    // -----------------------------------------------
    [ImpConfig] public static float grab_hold_time=0.2f; // time (sec) when attempting to grab an object for drag&drop to wait before confirming
    
    public static readonly TLabel INPUT_ACTION_GRAB = "_Grab";
    public static bool is_grabbing=false;
    public static object grab_object=null;

    public static void Update_Grab(float dt)
    {
        
    }
    

        
    // -----------------------------------------------
    // Poll
    // -----------------------------------------------

    // Samples every mapped key once per frame, so all the Key_/Action_ queries below agree
    // with each other no matter where in the frame they run. Driven by ImpApp.
    public static void Input_Poll(double dt)
    {
        foreach (var player in players) { player.Poll(dt); }
    }

    void Poll(double dt)
    {
        foreach (var key in keys_mapped)
        {
            float value = Key_Read(key);
            key_value[key] = value;

            //a key counts as held whenever it reads non-zero: that covers buttons (0 or 1),
            //analog sticks and pointer motion with one rule.
            bool is_down = MathF.Abs(value) > 0f;
            key_state.TryGetValue(key, out EInputState prev);
            bool was_down = prev is EInputState.Pressed or EInputState.Down;

            key_state[key] = is_down
                ? (was_down ? EInputState.Down : EInputState.Pressed)
                : (was_down ? EInputState.Released : EInputState.None);
        }

        Input_Dispatch(dt);
    }

    // Hands this frame's state changes to whatever registered for them.
    // Actions (not raw keys) are the dispatch unit, since one action can be bound
    // across several keys (eg. "_Move" spanning W/A/S/D) that need to combine into
    // a single state and axis, mirroring Action_GetState/Action_GetAxis.
    void Input_Dispatch(double dt)
    {
        if (input_targets.Count == 0) return;

        var labels = new HashSet<TLabel>(input_natives.Keys);
        labels.UnionWith(input_actions.Keys);

        foreach (var label in labels)
        {
            var action = input_actions.GetValueOrDefault(label) ?? input_natives.GetValueOrDefault(label);
            if (action == null) continue;

            EInputState state = EInputState.None;
            Vector3 axis = Vector3.Zero;

            foreach (var (key, binding) in action.keys)
            {
                EInputState keyState = key_state.GetValueOrDefault(key, EInputState.None);
                if (keyState == EInputState.Pressed) state = EInputState.Pressed;
                else if (keyState == EInputState.Down && state != EInputState.Pressed) state = EInputState.Down;
                else if (keyState == EInputState.Released && state == EInputState.None) state = EInputState.Released;

                float value = key_value.GetValueOrDefault(key, 0f);
                if (MathF.Abs(value) < binding.deadzone) continue;
                axis += binding.axis_scale * value;
            }

            if (state == EInputState.None) continue;

            foreach (var target in input_targets)
            {
                if (!target.Input_IsEnabled()) continue;

                switch (state)
                {
                    case EInputState.Pressed: target.Input_OnPressed(action, axis); break;
                    case EInputState.Down: target.Input_OnUpdate(action, axis,dt); break;
                    case EInputState.Released: target.Input_OnReleased(action, axis); break;
                }
            }
        }
    }

    // Raw reading for one key: 0/1 for buttons, signed for sticks and pointer motion.
    float Key_Read(EInputKey key)
    {
        if (map_keyboard.TryGetValue(key, out var kb)) return Raylib.IsKeyDown(kb) ? 1f : 0f;
        if (map_mouse.TryGetValue(key, out var mb)) return Raylib.IsMouseButtonDown(mb) ? 1f : 0f;
        if (map_pad.TryGetValue(key, out var pb)) return Raylib.IsGamepadButtonDown(player_id, pb) ? 1f : 0f;
        if (map_stick.TryGetValue(key, out var ax)) return Raylib.GetGamepadAxisMovement(player_id, ax);

        return key switch
        {
            EInputKey.Mouse_MoveX     => Raylib.GetMouseDelta().X,
            EInputKey.Mouse_MoveY     => Raylib.GetMouseDelta().Y,
            EInputKey.Mouse_WheelUp   => MathF.Max(0f,  Raylib.GetMouseWheelMove()),
            EInputKey.Mouse_WheelDown => MathF.Max(0f, -Raylib.GetMouseWheelMove()),
            _                         => 0f,
        };
    }

    // -----------------------------------------------
    // Key
    // -----------------------------------------------

    public static bool Key_IsDown(EInputKey key, byte player_id = 0)
    {
        return Key_GetState(key, player_id) is EInputState.Pressed or EInputState.Down;
    }

    public static bool Key_JustPressed(EInputKey key, byte player_id = 0)
    {
        return Key_GetState(key, player_id) == EInputState.Pressed;
    }

    public static bool Key_JustReleased(EInputKey key, byte player_id = 0)
    {
        return Key_GetState(key, player_id) == EInputState.Released;
    }

    public static EInputState Key_GetState(EInputKey key, byte player_id = 0)
    {
        if (player_id >= players.Count) return EInputState.None;
        return players[player_id].key_state.GetValueOrDefault(key, EInputState.None);
    }

    // Analog reading for a single key, before any action's deadzone or axis mapping.
    public static float Key_GetValue(EInputKey key, byte player_id = 0)
    {
        if (player_id >= players.Count) return 0f;
        return players[player_id].key_value.GetValueOrDefault(key, 0f);
    }

    // -----------------------------------------------
    // Actions
    // -----------------------------------------------

    // A player's own bindings win over the engine defaults, so rebinding one action
    // leaves the rest working.
    public static TInputAction? Action_Find(TLabel action, byte player_id = 0)
    {
        if (player_id >= players.Count) return null;

        var bound = players[player_id].input_actions.GetValueOrDefault(action);
        return bound ?? input_natives.GetValueOrDefault(action);
    }

    // The strongest state across every key bound to the action, so an action held on one
    // key while another is tapped still reads as held.
    public static EInputState Action_GetState(TLabel action, byte player_id = 0)
    {
        var bound = Action_Find(action, player_id);
        if (bound == null) return EInputState.None;

        EInputState best = EInputState.None;
        foreach (var key in bound.keys.Keys)
        {
            EInputState state = Key_GetState(key, player_id);
            if (state == EInputState.Pressed) return EInputState.Pressed;
            if (state == EInputState.Down) best = EInputState.Down;
            else if (state == EInputState.Released && best == EInputState.None) best = EInputState.Released;
        }

        return best;
    }

    // Sums each bound key's reading onto the axes its binding names. Keys under their
    // binding's deadzone contribute nothing.
    public static Vector3 Action_GetAxis(TLabel action, byte player_id = 0)
    {
        var bound = Action_Find(action, player_id);
        if (bound == null) return Vector3.Zero;

        Vector3 axis = Vector3.Zero;
        foreach (var (key, binding) in bound.keys)
        {
            float value = Key_GetValue(key, player_id);
            if (MathF.Abs(value) < binding.deadzone) continue;

            axis += binding.axis_scale * value;
        }

        return axis;
    }

    public static bool Action_IsDown(TLabel action, byte player_id = 0)
    {
        return Action_GetState(action, player_id) is EInputState.Pressed or EInputState.Down;
    }

    public static bool Action_JustPressed(TLabel action, byte player_id = 0)
    {
        return Action_GetState(action, player_id).Equals(EInputState.Pressed);
    }

    public static bool Action_JustReleased(TLabel action, byte player_id = 0)
    {
        return Action_GetState(action, player_id).Equals(EInputState.Released);
    }

    // -----------------------------------------------
    // Key tables
    // -----------------------------------------------

    // EInputKey's keyboard names mirror raylib's, so the table is derived by name with a
    // short exception list rather than spelled out key by key. Anything that fails to map
    // simply never reports as down.
    static readonly Dictionary<EInputKey, KeyboardKey> map_keyboard = Map_Keyboard();

    static readonly Dictionary<EInputKey, MouseButton> map_mouse = new()
    {
        [EInputKey.Mouse_Left]   = MouseButton.Left,
        [EInputKey.Mouse_Right]  = MouseButton.Right,
        [EInputKey.Mouse_Middle] = MouseButton.Middle,
        [EInputKey.Mouse_Side1]  = MouseButton.Back,
        [EInputKey.Mouse_Side2]  = MouseButton.Forward,
    };

    static readonly Dictionary<EInputKey, GamepadButton> map_pad = new()
    {
        [EInputKey.Pad_FaceDown]     = GamepadButton.RightFaceDown,
        [EInputKey.Pad_FaceRight]    = GamepadButton.RightFaceRight,
        [EInputKey.Pad_FaceLeft]     = GamepadButton.RightFaceLeft,
        [EInputKey.Pad_FaceUp]       = GamepadButton.RightFaceUp,

        [EInputKey.Pad_DPadUp]       = GamepadButton.LeftFaceUp,
        [EInputKey.Pad_DPadDown]     = GamepadButton.LeftFaceDown,
        [EInputKey.Pad_DPadLeft]     = GamepadButton.LeftFaceLeft,
        [EInputKey.Pad_DPadRight]    = GamepadButton.LeftFaceRight,

        [EInputKey.Pad_LeftBumper]   = GamepadButton.LeftTrigger1,
        [EInputKey.Pad_RightBumper]  = GamepadButton.RightTrigger1,
        [EInputKey.Pad_LeftTrigger]  = GamepadButton.LeftTrigger2,
        [EInputKey.Pad_RightTrigger] = GamepadButton.RightTrigger2,

        [EInputKey.Pad_LeftThumb]    = GamepadButton.LeftThumb,
        [EInputKey.Pad_RightThumb]   = GamepadButton.RightThumb,

        [EInputKey.Pad_Start]        = GamepadButton.MiddleRight,
        [EInputKey.Pad_Select]       = GamepadButton.MiddleLeft,
        [EInputKey.Pad_Home]         = GamepadButton.Middle,
    };

    static readonly Dictionary<EInputKey, GamepadAxis> map_stick = new()
    {
        [EInputKey.Stick_AxisX]  = GamepadAxis.LeftX,
        [EInputKey.Stick_AxisY]  = GamepadAxis.LeftY,
        [EInputKey.Stick_AxisRx] = GamepadAxis.RightX,
        [EInputKey.Stick_AxisRy] = GamepadAxis.RightY,
        [EInputKey.Stick_AxisZ]  = GamepadAxis.LeftTrigger,
        [EInputKey.Stick_AxisRz] = GamepadAxis.RightTrigger,
    };

    // Every key any of the tables can read, so polling doesn't walk the whole enum.
    static readonly EInputKey[] keys_mapped = Keys_Mapped();

    static Dictionary<EInputKey, KeyboardKey> Map_Keyboard()
    {
        //raylib spells the number row out and prefixes the context-menu key
        var exceptions = new Dictionary<string, KeyboardKey>
        {
            ["0"] = KeyboardKey.Zero,  ["1"] = KeyboardKey.One,   ["2"] = KeyboardKey.Two,
            ["3"] = KeyboardKey.Three, ["4"] = KeyboardKey.Four,  ["5"] = KeyboardKey.Five,
            ["6"] = KeyboardKey.Six,   ["7"] = KeyboardKey.Seven, ["8"] = KeyboardKey.Eight,
            ["9"] = KeyboardKey.Nine,
            ["Menu"] = KeyboardKey.KeyboardMenu,
        };

        var map = new Dictionary<EInputKey, KeyboardKey>();
        foreach (EInputKey key in Enum.GetValues<EInputKey>())
        {
            string name = key.ToString();
            if (!name.StartsWith("Key_")) continue;
            name = name["Key_".Length..];

            if (exceptions.TryGetValue(name, out KeyboardKey special)) map[key] = special;
            else if (Enum.TryParse(name, out KeyboardKey parsed)) map[key] = parsed;
        }

        return map;
    }

    static EInputKey[] Keys_Mapped()
    {
        var keys = new List<EInputKey>(map_keyboard.Keys);
        keys.AddRange(map_mouse.Keys);
        keys.AddRange(map_pad.Keys);
        keys.AddRange(map_stick.Keys);
        keys.AddRange([EInputKey.Mouse_MoveX, EInputKey.Mouse_MoveY,
                       EInputKey.Mouse_WheelUp, EInputKey.Mouse_WheelDown]);

        return keys.ToArray();
    }

    // =====================================================================================
    // CLASS
    // =====================================================================================
    public ImpComp3D? pawn;
    public byte player_id;
    public ImpComp2D? focus_widget;
    public List<I_InputTarget> input_targets = new List<I_InputTarget>();
    public Dictionary<EInputKey, EInputState> key_state = new Dictionary<EInputKey, EInputState>();
    public Dictionary<EInputKey, float> key_value = new Dictionary<EInputKey, float>();
}
