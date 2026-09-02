using Engine.Assets.General;

namespace Engine.Interfaces;

public struct TAttributeModifier
{
    [ImpVar] public AG_Attribute attribute;
    [ImpVar] public float increment;
    [ImpVar] public float multiplier;
}



// a a data source for a creature
public interface I_Creature
{
    public virtual List<TAttributeModifier> Creature_GetAttributeModifiers() { return null; }
}