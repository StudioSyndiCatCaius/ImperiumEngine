using Engine.Comps._1D;
using Engine.Structs;

namespace Engine.Assets.Effects;

public class Eff_AuraRemove : A_Effect
{
    [ImpVar] public List<TClass<C1_Aura>> removed_classes;
    [ImpVar] public TTagSet removed_tags;
    [ImpVar] public bool remove_all;

    public override void OnApply(C1_Creature target, C1_Creature instigator)
    {
        if (remove_all)
        {
            target.Aura_Remove_All();
        }
        else
        {
            foreach (var c in removed_classes)
            {
                target.Aura_Remove_OfClass(c,true);
            }
            target.Aura_Remove_AllOfTag(removed_tags);
        }
    }
}