using ImperiumEngine.Comps._1D;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Assets.Effects;

public class Eff_AuraAdd : A_Effect
{
    [ImpVar] public TClass<C1_Aura> aura_class;
    [ImpVar] public ImpAsset context;

    public override void OnApply(C1_Creature target, C1_Creature instigator)
    {
        target.Aura_Add(aura_class, instigator, context);
    }
}