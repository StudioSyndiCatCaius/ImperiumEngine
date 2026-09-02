namespace Engine.Core;

public enum EModuleType
{
    Game, //content in the main game folder
    Engine, //content packaged with the engine & editor 
    Mod //content packaged with a mod
}

// both a Module AND a Modification. this is a modular package of game content & assets
public class ImpMod
{
    public string name;
    public string id;
    public EModuleType type;
}