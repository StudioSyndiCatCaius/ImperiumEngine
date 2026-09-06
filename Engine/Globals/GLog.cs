using Raylib_cs;

namespace Engine.Globals;

public struct TLogEntry
{
    public string text;
    public Color color;
    public double time;
}

public static class GLog
{
    public static readonly List<TLogEntry> entries = new();
    const int MaxEntries = 400;

    public static void Log(string msg, bool on_screen, Color color=default)
    {
        Console.WriteLine(msg);
        Console.Out.Flush();
        if (color.A == 0) color = Color.LightGray;
        entries.Add(new TLogEntry { text = msg ?? "", color = color, time = Raylib.GetTime() });
        if (entries.Count > MaxEntries)
            entries.RemoveRange(0, entries.Count - MaxEntries);
    }

    public static void Clear() { entries.Clear(); }

    [ScriptCall] public static void Info(string msg,bool on_screen=false) {
        Log(msg, on_screen); }

    [ScriptCall] public static void Warning(string msg,bool on_screen=false) {
        Log("WARNING: "+msg, on_screen,Color.Yellow); }

    [ScriptCall] public static void Error(string msg,bool on_screen=false) {
        Log("ERROR: "+msg, on_screen,Color.Red); }
}