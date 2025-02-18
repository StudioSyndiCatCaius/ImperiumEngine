using ImperiumEngine.Source.Assets;

namespace ImperiumEngine.Source.Objects._1D;

public class O1D_Equipment: ImpComponent1D
{
    private Dictionary<IA_EquipmentSlot, ImpObject> _equipment;

    public void UnequipSlot(IA_EquipmentSlot slot)
    {
        
    }
    
    public void EquipToSlot(IA_EquipmentSlot slot, ImpObject item)
    {  
        UnequipSlot(slot);
        _equipment[slot] = item;
    }
}

public class IA_EquipmentSlot : IA_GameplayCommon
{
    public FGameplayTagContainer AcceptedTags;
    public FGameplayTagContainer RejectedTags;
}

public interface IEquippable
{
    bool CanEquip(O1D_Equipment Component, IA_EquipmentSlot Slot);
}
