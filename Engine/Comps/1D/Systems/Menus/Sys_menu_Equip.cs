using Engine.Comps._2D;
using Engine.Core;

namespace Engine.Comps._1D.States.Menus;



public class Sys_menu_Equip : C1_System
{
    [ImpVar] public C2_OptionList list_party;
    [ImpVar] public C2_OptionList list_slots;
    [ImpVar] public C2_OptionList list_items;
    
    const byte SUBSTATE_MEMBERS = 0;
    const byte SUBSTATE_SLOT = 1;
    const byte SUBSTATE_ITEM = 2;

    public ImpAsset current_pm;
    public ImpAsset current_slot;
    
    public C1_Creature pm_creature;

    public override void OnBegin()
    {
        base.OnBegin();
        // ----- SELECT CHARACTER -----
        if (list_party != null)
        {
            list_items.on_option_select = (d, i) =>
            {
                if (d.option_data is ImpAsset)
                {
                    current_pm=d.option_data as ImpAsset;
                    substate = SUBSTATE_SLOT;
                }
            };
        }
        // ----- SELECT SLOT -----
        if (list_slots != null)
        {
            list_slots.on_option_select = (d, i) =>
            {
                if (d.option_data is ImpAsset)
                {
                    current_slot=d.option_data as ImpAsset;
                    substate = SUBSTATE_ITEM;
                }
            };
        }
        // ----- SELECT ITEM -----
        if (list_items != null)
        {
            list_items.on_option_select = (d, i) =>
            {
                if (d.option_data is ImpAsset)
                {
                    pm_creature.Equipment_Equip(current_slot, d.option_data as ImpAsset);
                    substate = SUBSTATE_SLOT;
                }
            };
        }
    }
}