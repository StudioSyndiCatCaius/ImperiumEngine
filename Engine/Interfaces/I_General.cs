using Engine.Assets;
using Engine.Structs;
using Raylib_cs;

namespace Engine.Interfaces;

public interface I_General
{
    public string getTitle() { return "";}
    public string getDescription() { return "";}
    public A_Texture getIcon() { return null; }
    public TTagSet getTags() { return default; }
}