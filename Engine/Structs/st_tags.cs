using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;

public struct TTag : I_PropertyType
{
    //this is a string for now but later look into some sort of bit reference.
    public string tagString { get; set; }
    
    public TTag(string tag)
    {
        tagString = tag;
    }
}

public struct TTagSet : I_PropertyType
{
    List<TTag> tags;

    public bool Add(TTag tag)
    {
        return false;
    }
}


public abstract class TagCollection
{
    public virtual List<string> DefineTags()
    {
        return new List<string>();
    }
}