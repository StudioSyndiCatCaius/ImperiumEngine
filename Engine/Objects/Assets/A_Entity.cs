using ImperiumEngine.Classes;

namespace ImperiumEngine.Objects.Assets;

// a preset of components in a heriarchy. entities themselves then are treated like a new component (a child of whatever their given parent type is)
public class A_Entity : ImpAsset
{
    public string parent_type = "";
    public List<ImpComponent> components = new();
}
