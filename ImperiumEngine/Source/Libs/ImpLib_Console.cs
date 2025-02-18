namespace ImperiumEngine.Source.Libs;

public static class ImpLib_Console
{
    public static void Console_Log(string str, bool toLog = true, bool toScreen = false)
    {
        Console.WriteLine(str);
    }
}