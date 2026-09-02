using Raylib_cs;

namespace Engine.Globals;

public static class GLog
{
    public static void Log(string msg, bool on_screen, Color color=default)
    {
        Console.WriteLine(msg);
        Console.Out.Flush();
    }

    [ScriptCall] public static void Info(string msg,bool on_screen=false) {
        Log(msg, on_screen); }

    [ScriptCall] public static void Warning(string msg,bool on_screen=false) {
        Log("WARNING: "+msg, on_screen,Color.Yellow); }

    [ScriptCall] public static void Error(string msg,bool on_screen=false) {
        Log("ERROR: "+msg, on_screen,Color.Red); }
}