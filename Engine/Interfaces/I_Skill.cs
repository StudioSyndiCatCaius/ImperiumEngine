using ImperiumEngine.Assets;
using ImperiumEngine.Assets.General;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Interfaces;

public struct TSkillConfig
{
    [ImpVar] public A_TargetFilter target_filter;
    [ImpVar] public Dictionary<AG_Attribute,float> attribute_cost;
}

public interface I_Skill
{
    public virtual TSkillConfig Skill_GetConfig() { return default;}
    
    public virtual double Skill_GetTargetUtility(C1_Creature target, C1_Creature instigator) { return 0;}
    public virtual List<A_Effect> Skill_GetEffects(TTag tag) { return null; }
    public virtual void Skill_OnApplied(C1_Creature target, C1_Creature instigator, TTag tag) { }
    
    // ===============================================================================================================
    // Static
    // ===============================================================================================================

    public static bool CanUse(I_Skill skill, C1_Creature instigator)
    {
        if(!instigator.Attributes_HasMinimum(skill.Skill_GetConfig().attribute_cost)) return false;
        return true;
    }

    public static void ConsumeAttributeCost(I_Skill skill, C1_Creature instigator)
    {
        
    }
    
    public static void ApplyToTarget(I_Skill skill, C1_Creature target, C1_Creature instigator, TTag tag)
    {
        foreach (var effect in skill.Skill_GetEffects(tag))
        {
            effect.OnApply(target, instigator);
        }
        skill.Skill_OnApplied(target, instigator, tag);
    }
        
}