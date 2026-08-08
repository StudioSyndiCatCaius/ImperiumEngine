using ImperiumEngine.Assets;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Interfaces;

public interface I_General
{
    public virtual TText gTitle() { return ""; }
    public virtual A_Texture gIcon() { return null; }
    public virtual TText gDescription() { return ""; }
    public virtual TTagSet gTags() { return new(); }
}