namespace ImperiumEngine.Interfaces;

public interface I_Equipable 
{
    public virtual bool Equipable_CanEquip(ImpAsset slot) => true;
    
}