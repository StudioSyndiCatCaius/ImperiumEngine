using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

public class FTag : I_PropertyType
{
    //this is a string for now but later look into some sort of bit reference.
    public string tagString { get; set; }
}

public class FTagSet : I_PropertyType
{
    List<FTag> tags;
}