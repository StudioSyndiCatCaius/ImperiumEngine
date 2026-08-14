namespace ImperiumEngine;


//baseclass for all controllers devices (including keyboard, mouse, etc.)
public class ImpController
{
    const byte ID_KEYBOARD=0;
    const byte ID_MOUSE=1;
    const byte ID_GAMEPAD_1=2;
    const byte ID_GAMEPAD_2=3;
    const byte ID_GAMEPAD_3=4;
    const byte ID_GAMEPAD_4=5;
    const byte ID_GAMEPAD_5=6;
    const byte ID_GAMEPAD_6=7;
    const byte ID_GAMEPAD_7=8;
    const byte ID_GAMEPAD_8=9;
    
    const int MAX_CONTROLLERS=10;
    
    // #################################################################################
    // Statics
    // #################################################################################
    static bool[] was_connected=new bool[MAX_CONTROLLERS];
    
    public readonly static List<ImpController> controllers;
    
    public static Action<ImpController> on_connected;
    public static Action<ImpController> on_disconnected;
    
    
        
    // #################################################################################
    // Class
    // #################################################################################

    //
    public byte id;
}