using ImperiumEngine.Objects.Assets;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Interfaces;

public interface I_General
{
    public virtual TText getTitle() { return new TText("");}
    public virtual TText getDescription() { return new TText("");}
    public virtual A_Texture2D getIcon() { return null;}
    public virtual TTags getTags() { return new TTags(); }
}