using Engine.Core;

namespace Engine.Interfaces;

public interface I_Equipable 
{
    public virtual bool Equipable_CanEquip(ImpAsset slot) => true;
    
}