using ImperiumEngine.Comps._2D;

namespace ImperiumEngine.Interfaces;

public interface I_Property
{
    public virtual bool Inspector_IsCustom() => false;
    public virtual void Inspector_Rebuild(C2_InspectorProperty prop_ui) { }
}
