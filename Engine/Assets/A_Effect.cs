using Engine.Comps._1D;
using Engine.Core;

namespace Engine.Assets;

//an effect that can be applied to a creature
public abstract class A_Effect : ImpAsset
{
    public virtual void OnApply(C1_Creature target, C1_Creature instigator) {}
}