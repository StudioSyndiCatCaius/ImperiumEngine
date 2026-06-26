namespace ImperiumEngine.Interfaces;

public interface I_Saveable
{
    public void Savable_OnRead(string path) { }
    public void Savable_OnWrite(string path) { }
}