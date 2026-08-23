using ImperiumEngine.Assets.General;
using ImperiumEngine.Comps._1D;

namespace ImperiumEngine.Assets.Effects;

public class Eff_DamageFlat : A_Effect
{
    [ImpVar] public AG_Attribute damaged_attribute;
    [ImpVar] public float damage_amount;
    [ImpVar] public bool is_percent;
    [ImpVar] public AG_DamageType damage_type;
    
    public override void OnApply(C1_Creature target, C1_Creature instigator)
    {
        float in_damage = damage_amount;
        if (is_percent)
        {
            in_damage = target.Attribute_Get_Max(damaged_attribute) * damage_amount;
        }
        target.Attribute_Damage(damaged_attribute,in_damage, instigator, damage_type);
    }
}