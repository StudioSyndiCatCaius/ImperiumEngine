namespace ImperiumCore.Const;


// Key code constants — values match GLFW/Silk.NET's Key enum so they can be
// compared directly against Silk.NET input events without casting.
public static class ImpC_Keys
{
    // -------------------------------------------------------------------------
    // Printable — ASCII range
    // -------------------------------------------------------------------------

    public const uint Space        = 32;
    public const uint Apostrophe   = 39;   // '
    public const uint Comma        = 44;   // ,
    public const uint Minus        = 45;   // -
    public const uint Period       = 46;   // .
    public const uint Slash        = 47;   // /

    public const uint Num0 = 48;
    public const uint Num1 = 49;
    public const uint Num2 = 50;
    public const uint Num3 = 51;
    public const uint Num4 = 52;
    public const uint Num5 = 53;
    public const uint Num6 = 54;
    public const uint Num7 = 55;
    public const uint Num8 = 56;
    public const uint Num9 = 57;

    public const uint Semicolon    = 59;   // ;
    public const uint Equal        = 61;   // =

    public const uint A = 65;
    public const uint B = 66;
    public const uint C = 67;
    public const uint D = 68;
    public const uint E = 69;
    public const uint F = 70;
    public const uint G = 71;
    public const uint H = 72;
    public const uint I = 73;
    public const uint J = 74;
    public const uint K = 75;
    public const uint L = 76;
    public const uint M = 77;
    public const uint N = 78;
    public const uint O = 79;
    public const uint P = 80;
    public const uint Q = 81;
    public const uint R = 82;
    public const uint S = 83;
    public const uint T = 84;
    public const uint U = 85;
    public const uint V = 86;
    public const uint W = 87;
    public const uint X = 88;
    public const uint Y = 89;
    public const uint Z = 90;

    public const uint LeftBracket  = 91;   // [
    public const uint Backslash    = 92;   // \
    public const uint RightBracket = 93;   // ]
    public const uint GraveAccent  = 96;   // `

    // -------------------------------------------------------------------------
    // Control / editing
    // -------------------------------------------------------------------------

    public const uint Escape       = 256;
    public const uint Enter        = 257;
    public const uint Tab          = 258;
    public const uint Backspace    = 259;
    public const uint Insert       = 260;
    public const uint Delete       = 261;

    // -------------------------------------------------------------------------
    // Navigation
    // -------------------------------------------------------------------------

    public const uint Right        = 262;
    public const uint Left         = 263;
    public const uint Down         = 264;
    public const uint Up           = 265;
    public const uint PageUp       = 266;
    public const uint PageDown     = 267;
    public const uint Home         = 268;
    public const uint End          = 269;

    // -------------------------------------------------------------------------
    // Lock / system
    // -------------------------------------------------------------------------

    public const uint CapsLock     = 280;
    public const uint ScrollLock   = 281;
    public const uint NumLock      = 282;
    public const uint PrintScreen  = 283;
    public const uint Pause        = 284;

    // -------------------------------------------------------------------------
    // Function keys
    // -------------------------------------------------------------------------

    public const uint F1  = 290;
    public const uint F2  = 291;
    public const uint F3  = 292;
    public const uint F4  = 293;
    public const uint F5  = 294;
    public const uint F6  = 295;
    public const uint F7  = 296;
    public const uint F8  = 297;
    public const uint F9  = 298;
    public const uint F10 = 299;
    public const uint F11 = 300;
    public const uint F12 = 301;
    public const uint F13 = 302;
    public const uint F14 = 303;
    public const uint F15 = 304;
    public const uint F16 = 305;
    public const uint F17 = 306;
    public const uint F18 = 307;
    public const uint F19 = 308;
    public const uint F20 = 309;
    public const uint F21 = 310;
    public const uint F22 = 311;
    public const uint F23 = 312;
    public const uint F24 = 313;
    public const uint F25 = 314;

    // -------------------------------------------------------------------------
    // Numpad
    // -------------------------------------------------------------------------

    public const uint KP0        = 320;
    public const uint KP1        = 321;
    public const uint KP2        = 322;
    public const uint KP3        = 323;
    public const uint KP4        = 324;
    public const uint KP5        = 325;
    public const uint KP6        = 326;
    public const uint KP7        = 327;
    public const uint KP8        = 328;
    public const uint KP9        = 329;
    public const uint KPDecimal  = 330;
    public const uint KPDivide   = 331;
    public const uint KPMultiply = 332;
    public const uint KPSubtract = 333;
    public const uint KPAdd      = 334;
    public const uint KPEnter    = 335;
    public const uint KPEqual    = 336;

    // -------------------------------------------------------------------------
    // Modifiers
    // -------------------------------------------------------------------------

    public const uint LeftShift    = 340;
    public const uint LeftCtrl     = 341;
    public const uint LeftAlt      = 342;
    public const uint LeftSuper    = 343;   // Windows / Cmd
    public const uint RightShift   = 344;
    public const uint RightCtrl    = 345;
    public const uint RightAlt     = 346;
    public const uint RightSuper   = 347;
    public const uint Menu         = 348;

    // -------------------------------------------------------------------------
    // Mouse buttons
    // -------------------------------------------------------------------------

    public const uint Mouse1 = 400;   // Left
    public const uint Mouse2 = 401;   // Right
    public const uint Mouse3 = 402;   // Middle
    public const uint Mouse4 = 403;
    public const uint Mouse5 = 404;
    public const uint Mouse6 = 405;
    public const uint Mouse7 = 406;
    public const uint Mouse8 = 407;

    public const uint MouseLeft   = Mouse1;
    public const uint MouseRight  = Mouse2;
    public const uint MouseMiddle = Mouse3;

    // -------------------------------------------------------------------------
    // Gamepad buttons  (Xbox / generic standard layout)
    // -------------------------------------------------------------------------

    public static class Gamepad
    {
        public const uint A           = 500;
        public const uint B           = 501;
        public const uint X           = 502;
        public const uint Y           = 503;
        public const uint LB          = 504;   // Left bumper
        public const uint RB          = 505;   // Right bumper
        public const uint Back        = 506;
        public const uint Start       = 507;
        public const uint Guide       = 508;   // Xbox / PS button
        public const uint L3          = 509;   // Left stick click
        public const uint R3          = 510;   // Right stick click
        public const uint DPadUp      = 511;
        public const uint DPadRight   = 512;
        public const uint DPadDown    = 513;
        public const uint DPadLeft    = 514;

        // PlayStation aliases
        public const uint Cross    = A;
        public const uint Circle   = B;
        public const uint Square   = X;
        public const uint Triangle = Y;
        public const uint L1       = LB;
        public const uint R1       = RB;
        public const uint Options  = Start;
        public const uint TouchPad = 515;

        // Axes — index passed to GetAxis()
        public static class Axis
        {
            public const uint LeftX        = 0;
            public const uint LeftY        = 1;
            public const uint RightX       = 2;
            public const uint RightY       = 3;
            public const uint LeftTrigger  = 4;
            public const uint RightTrigger = 5;

            // PlayStation aliases
            public const uint L2 = LeftTrigger;
            public const uint R2 = RightTrigger;
        }
    }
}
