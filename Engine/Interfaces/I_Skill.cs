using ImperiumEngine.Assets.General;
using ImperiumEngine.Comps._1D;

namespace ImperiumEngine.Interfaces;

public struct TSkillConfig
{
    [ImpVar] public A_TargetFilter target_filter;
    [ImpVar] public Dictionary<AG_Attribute,float> attribute_modifiers;
}

public interface I_Skill
{
    public virtual TSkillConfig? Skill_GetConfig() { return null;}
    
    public virtual double Skill_GetTargetUtility(C1_Creature target, C1_Creature instigator) { return 0;}
}