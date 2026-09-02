namespace Engine.Assets.General;

public class AG_EquipType : A_General
{
    [ImpVar] public AG_EquipType parent_type;
    
    // ==============================================================================================================
    // STATIC
    // ==============================================================================================================
    
    public static AG_EquipType TYPE_WEAPON = new();
    public static AG_EquipType TYPE_ARMOR = new();
    public static AG_EquipType TYPE_ACCESSORY = new();
    
}