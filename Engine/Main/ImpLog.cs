
using Raylib_cs;

namespace ImperiumEngine.Main;

public class ImpLog
{
    public static void Log(string msg, bool to_screen = false, float screen_duration = 2.0f, Color color=default)
    {
        Console.WriteLine($"[ImpLog] {msg}");
    }

    public static void Error(string msg, bool to_screen = false, float screen_duration = 2.0f)
    {
        Log(msg, to_screen, screen_duration,Color.Red);
    }
    
    public static void Warning(string msg, bool to_screen = false, float screen_duration = 2.0f)
    {
        Log(msg, to_screen, screen_duration,Color.Yellow);
    }
}