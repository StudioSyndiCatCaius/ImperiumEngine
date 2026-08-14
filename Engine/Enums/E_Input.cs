using Raylib_cs;

namespace ImperiumEngine.Enums;



public enum EInputState
{
    None=0, 
    Pressed=1, 
    Down=2, 
    Released=3,
}

/// <summary>
/// Unified input codes.
/// Keyboard values are identical to Raylib.KeyboardKey / GetKeyPressed().
/// Everything else uses non-overlapping artificial ranges.
/// </summary>
public enum EInputKey
{
    None = 0,

    // -----------------------------------------------------------------
    // Keyboard  (exact Raylib.KeyboardKey values)
    // -----------------------------------------------------------------

    // Alphanumeric / punctuation
    Key_Apostrophe      = 39,   // '
    Key_Comma           = 44,   // ,
    Key_Minus           = 45,   // -
    Key_Period          = 46,   // .
    Key_Slash           = 47,   // /
    Key_0               = 48,
    Key_1               = 49,
    Key_2               = 50,
    Key_3               = 51,
    Key_4               = 52,
    Key_5               = 53,
    Key_6               = 54,
    Key_7               = 55,
    Key_8               = 56,
    Key_9               = 57,
    Key_Semicolon       = 59,   // ;
    Key_Equal           = 61,   // =
    Key_A               = 65,
    Key_B               = 66,
    Key_C               = 67,
    Key_D               = 68,
    Key_E               = 69,
    Key_F               = 70,
    Key_G               = 71,
    Key_H               = 72,
    Key_I               = 73,
    Key_J               = 74,
    Key_K               = 75,
    Key_L               = 76,
    Key_M               = 77,
    Key_N               = 78,
    Key_O               = 79,
    Key_P               = 80,
    Key_Q               = 81,
    Key_R               = 82,
    Key_S               = 83,
    Key_T               = 84,
    Key_U               = 85,
    Key_V               = 86,
    Key_W               = 87,
    Key_X               = 88,
    Key_Y               = 89,
    Key_Z               = 90,
    Key_LeftBracket     = 91,   // [
    Key_Backslash       = 92,   // \
    Key_RightBracket    = 93,   // ]
    Key_Grave           = 96,   // `

    // Function / navigation
    Key_Space           = 32,
    Key_Escape          = 256,
    Key_Enter           = 257,
    Key_Tab             = 258,
    Key_Backspace       = 259,
    Key_Insert          = 260,
    Key_Delete          = 261,
    Key_Right           = 262,
    Key_Left            = 263,
    Key_Down            = 264,
    Key_Up              = 265,
    Key_PageUp          = 266,
    Key_PageDown        = 267,
    Key_Home            = 268,
    Key_End             = 269,
    Key_CapsLock        = 280,
    Key_ScrollLock      = 281,
    Key_NumLock         = 282,
    Key_PrintScreen     = 283,
    Key_Pause           = 284,

    Key_F1              = 290,
    Key_F2              = 291,
    Key_F3              = 292,
    Key_F4              = 293,
    Key_F5              = 294,
    Key_F6              = 295,
    Key_F7              = 296,
    Key_F8              = 297,
    Key_F9              = 298,
    Key_F10             = 299,
    Key_F11             = 300,
    Key_F12             = 301,

    // Modifiers
    Key_LeftShift       = 340,
    Key_LeftControl     = 341,
    Key_LeftAlt         = 342,
    Key_LeftSuper       = 343,
    Key_RightShift      = 344,
    Key_RightControl    = 345,
    Key_RightAlt        = 346,
    Key_RightSuper      = 347,
    Key_Menu            = 348,  // KEY_KB_MENU

    // Keypad
    Key_Kp0             = 320,
    Key_Kp1             = 321,
    Key_Kp2             = 322,
    Key_Kp3             = 323,
    Key_Kp4             = 324,
    Key_Kp5             = 325,
    Key_Kp6             = 326,
    Key_Kp7             = 327,
    Key_Kp8             = 328,
    Key_Kp9             = 329,
    Key_KpDecimal       = 330,
    Key_KpDivide        = 331,
    Key_KpMultiply      = 332,
    Key_KpSubtract      = 333,
    Key_KpAdd           = 334,
    Key_KpEnter         = 335,
    Key_KpEqual         = 336,

    // Android extras (present in raylib)
    Key_Back            = 4,
    Key_VolumeUp        = 24,
    Key_VolumeDown      = 25,

    // -----------------------------------------------------------------
    // Mouse – 1000 + Raylib.MouseButton
    // -----------------------------------------------------------------
    Mouse_Left      = 1000 + 0,   // MouseButton.Left
    Mouse_Right     = 1000 + 1,   // MouseButton.Right
    Mouse_Middle    = 1000 + 2,   // MouseButton.Middle
    Mouse_Side1     = 1000 + 6,   // MouseButton.Back
    Mouse_Side2     = 1000 + 5,   // MouseButton.Forward
    Mouse_WheelUp   = 1000 + 10,  // artificial (no raylib button)
    Mouse_WheelDown = 1000 + 11,

    // pointer delta axes (artificial — must not collide with buttons/wheel)
    Mouse_MoveX     = 1000 + 20,
    Mouse_MoveY     = 1000 + 21,

    // -----------------------------------------------------------------
    // Gamepad  (offset 2000)
    // -----------------------------------------------------------------
    Pad_FaceDown    = 2000 + (int)GamepadButton.RightFaceDown,   // 7
    Pad_FaceRight   = 2000 + (int)GamepadButton.RightFaceRight,  // 6
    Pad_FaceLeft    = 2000 + (int)GamepadButton.RightFaceLeft,   // 8
    Pad_FaceUp      = 2000 + (int)GamepadButton.RightFaceUp,     // 5

    Pad_DPadUp      = 2000 + (int)GamepadButton.LeftFaceUp,      // 1
    Pad_DPadRight   = 2000 + (int)GamepadButton.LeftFaceRight,   // 2
    Pad_DPadDown    = 2000 + (int)GamepadButton.LeftFaceDown,    // 3
    Pad_DPadLeft    = 2000 + (int)GamepadButton.LeftFaceLeft,    // 4

    Pad_LeftBumper  = 2000 + (int)GamepadButton.LeftTrigger1,    // 9
    Pad_RightBumper = 2000 + (int)GamepadButton.RightTrigger1,   // 11
    Pad_LeftTrigger = 2000 + (int)GamepadButton.LeftTrigger2,    // 10
    Pad_RightTrigger= 2000 + (int)GamepadButton.RightTrigger2,   // 12

    Pad_LeftThumb   = 2000 + (int)GamepadButton.LeftThumb,       // 15
    Pad_RightThumb  = 2000 + (int)GamepadButton.RightThumb,      // 16
    Pad_Select      = 2000 + (int)GamepadButton.MiddleLeft,      // 13
    Pad_Home        = 2000 + (int)GamepadButton.Middle,          // 14
    Pad_Start       = 2000 + (int)GamepadButton.MiddleRight,     // 15? wait – check actual values

    // sticks stay artificial
    Pad_LeftStickX  = 2050,
    Pad_LeftStickY  = 2051,
    Pad_RightStickX = 2052,
    Pad_RightStickY = 2053,

    // -----------------------------------------------------------------
    // Touch  (offset 3000)
    // -----------------------------------------------------------------
    Touch_0             = 3000,
    Touch_1             = 3001,
    Touch_2             = 3002,
    Touch_3             = 3003,
    Touch_4             = 3004,
    Touch_5             = 3005,
    Touch_6             = 3006,
    Touch_7             = 3007,
    Touch_8             = 3008,
    Touch_9             = 3009,

    // -----------------------------------------------------------------
    // Generic Joystick / raw stick (offset 4000)
    // -----------------------------------------------------------------
    Stick_Button0       = 4000,
    Stick_Button1       = 4001,
    Stick_Button2       = 4002,
    Stick_Button3       = 4003,
    Stick_Button4       = 4004,
    Stick_Button5       = 4005,
    Stick_Button6       = 4006,
    Stick_Button7       = 4007,
    Stick_Button8       = 4008,
    Stick_Button9       = 4009,
    Stick_Button10      = 4010,
    Stick_Button11      = 4011,
    Stick_Button12      = 4012,
    Stick_Button13      = 4013,
    Stick_Button14      = 4014,
    Stick_Button15      = 4015,

    Stick_AxisX         = 4100,
    Stick_AxisY         = 4101,
    Stick_AxisZ         = 4102,
    Stick_AxisRx        = 4103,
    Stick_AxisRy        = 4104,
    Stick_AxisRz        = 4105,
    Stick_AxisSlider    = 4106,

    Stick_HatUp         = 4200,
    Stick_HatDown       = 4201,
    Stick_HatLeft       = 4202,
    Stick_HatRight      = 4203,
}