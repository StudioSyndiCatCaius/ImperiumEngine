namespace ImperiumEngine.Source.Cores;

public class ImpLib_File
{
    public static string GetDirectory_Content()
    {
        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Content");
    }
    
    public static string GetFilepath_FromContent(string path)
    {
        return Path.Combine(GetDirectory_Content(), path);
    }
}