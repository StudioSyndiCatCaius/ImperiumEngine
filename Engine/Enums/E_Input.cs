namespace ImperiumEngine.Enums;



public enum EInputState
{
    None=0, 
    Pressed=1, 
    Down=2, 
    Released=3,
}

public enum EInputKey : byte
{
    None = 0,

    // ─────────────────────────────────────────────
    // Keyboard
    // ─────────────────────────────────────────────

    // Letters
    Key_A, Key_B, Key_C, Key_D, Key_E, Key_F, Key_G, Key_H, Key_I, Key_J,
    Key_K, Key_L, Key_M, Key_N, Key_O, Key_P, Key_Q, Key_R, Key_S, Key_T,
    Key_U, Key_V, Key_W, Key_X, Key_Y, Key_Z,

    // Number row
    Key_0, Key_1, Key_2, Key_3, Key_4, Key_5, Key_6, Key_7, Key_8, Key_9,

    // Function keys
    Key_F1, Key_F2, Key_F3, Key_F4, Key_F5, Key_F6,
    Key_F7, Key_F8, Key_F9, Key_F10, Key_F11, Key_F12,

    // Modifiers
    Key_LeftShift, Key_RightShift,
    Key_LeftControl, Key_RightControl,
    Key_LeftAlt, Key_RightAlt,
    Key_LeftSuper, Key_RightSuper,

    // Navigation
    Key_Up, Key_Down, Key_Left, Key_Right,
    Key_Home, Key_End, Key_PageUp, Key_PageDown,
    Key_Insert, Key_Delete,

    // Special
    Key_Escape, Key_Enter, Key_Space, Key_Tab, Key_Backspace,
    Key_CapsLock, Key_NumLock, Key_ScrollLock,
    Key_PrintScreen, Key_Pause, Key_Menu,

    // Numpad
    Key_Kp0, Key_Kp1, Key_Kp2, Key_Kp3, Key_Kp4,
    Key_Kp5, Key_Kp6, Key_Kp7, Key_Kp8, Key_Kp9,
    Key_KpDecimal, Key_KpDivide, Key_KpMultiply,
    Key_KpSubtract, Key_KpAdd, Key_KpEnter,

    // Punctuation
    Key_Apostrophe,     // '
    Key_Comma,          // ,
    Key_Minus,          // -
    Key_Period,         // .
    Key_Slash,          // /
    Key_Semicolon,      // ;
    Key_Equal,          // =
    Key_LeftBracket,    // [
    Key_Backslash,      // \
    Key_RightBracket,   // ]
    Key_Grave,          // `

    // ─────────────────────────────────────────────
    // Mouse
    // ─────────────────────────────────────────────

    Mouse_Left,
    Mouse_Right,
    Mouse_Middle,
    Mouse_Side1,        // Back
    Mouse_Side2,        // Forward
    Mouse_WheelUp,
    Mouse_WheelDown,

    // Pointer motion, read as a signed pixel delta for the frame rather than a on/off state
    Mouse_MoveX,
    Mouse_MoveY,

    // ─────────────────────────────────────────────
    // Gamepad
    // ─────────────────────────────────────────────

    Pad_FaceDown,       // A / Cross
    Pad_FaceRight,      // B / Circle
    Pad_FaceLeft,       // X / Square
    Pad_FaceUp,         // Y / Triangle

    Pad_DPadUp,
    Pad_DPadDown,
    Pad_DPadLeft,
    Pad_DPadRight,

    Pad_LeftBumper,
    Pad_RightBumper,
    Pad_LeftTrigger,
    Pad_RightTrigger,

    Pad_LeftThumb,
    Pad_RightThumb,

    Pad_Start,
    Pad_Select,
    Pad_Home,

    // ─────────────────────────────────────────────
    // Touch
    // ─────────────────────────────────────────────

    Touch_0,
    Touch_1,
    Touch_2,
    Touch_3,
    Touch_4,
    Touch_5,
    Touch_6,
    Touch_7,
    Touch_8,
    Touch_9,

    // ─────────────────────────────────────────────
    // Joystick
    // ─────────────────────────────────────────────

    Stick_Button0, Stick_Button1, Stick_Button2, Stick_Button3,
    Stick_Button4, Stick_Button5, Stick_Button6, Stick_Button7,
    Stick_Button8, Stick_Button9, Stick_Button10, Stick_Button11,
    Stick_Button12, Stick_Button13, Stick_Button14, Stick_Button15,

    Stick_AxisX,
    Stick_AxisY,
    Stick_AxisZ,
    Stick_AxisRx,
    Stick_AxisRy,
    Stick_AxisRz,
    Stick_AxisSlider,

    Stick_HatUp,
    Stick_HatDown,
    Stick_HatLeft,
    Stick_HatRight,
}