using Engine.Structs;

namespace Engine.Assets.General;

public struct TEquipSlotConditions
{
    [ImpVar] public TTagSet tags;
    [ImpVar] public List<AG_EquipType> equip_types;
}

public class AG_EquipSlot : A_General
{
    [ImpVar] public TEquipSlotConditions accepted_conditions;
    [ImpVar] public TEquipSlotConditions rejected_conditions;
    
    // ==============================================================================================================
    // STATIC
    // ==============================================================================================================
    
    public static AG_EquipSlot SLOT_WEAPON = new() { accepted_conditions = {equip_types = {AG_EquipType.TYPE_WEAPON}}};
    public static AG_EquipSlot SLOT_ARMOR = new() { accepted_conditions = { equip_types = { AG_EquipType.TYPE_ARMOR } }};
    public static AG_EquipSlot SLOT_ACCESSORY1 = new() { accepted_conditions = { equip_types = { AG_EquipType.TYPE_ACCESSORY } }};
    public static AG_EquipSlot SLOT_ACCESSORY2 = new() { accepted_conditions = { equip_types = { AG_EquipType.TYPE_ACCESSORY } }};
    public static AG_EquipSlot SLOT_ACCESSORY3 = new() { accepted_conditions = { equip_types = { AG_EquipType.TYPE_ACCESSORY } }};
}