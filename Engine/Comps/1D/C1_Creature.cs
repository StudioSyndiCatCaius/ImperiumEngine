using ImperiumEngine.Assets.General;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

// Creature is an advanced component for handling common gameplay functions for an entity (E.G, Attribute, Abilities, Equipment, Inventory, etc.)  
public class C1_Creature : ImpComp
{
    [ImpVar] public Imp3D creature_root; // intended to be the rootmost comp of this scene.
    
    private List<C1_Ability> _abilities;
    public A_CreatureConfig config;


    public override void OnBegin()
    {
        base.OnBegin();
        if (config == null) { config = new (); }
    }

    // ---------------------------------------------------------------------------------
    // Abilities
    // ---------------------------------------------------------------------------------
    
    //abilities granted OnBegin
    [ImpVar][Category("Abilities")] public List<TClass<C1_Ability>> auto_abilities;

    public void Ability_SetGranted(TClass<C1_Ability> ability, bool granted)
    {
        
    }
    
    public void Ability_Activate(TClass<C1_Ability> ability, object context)
    {
        
    }
    
    public void Ability_Stop(TClass<C1_Ability> ability)
    {
        
    }

    public C1_Ability? Ability_Get(TClass<C1_Ability> ability)
    {
        return null;
    }
    
    // ---------------------------------------------------------------------------------
    // Attributes
    // ---------------------------------------------------------------------------------
    
    //self, attribute, amount, instigator
    public Action<C1_Creature, AG_Attribute, float, C1_Creature> on_attribute_damage;
    
    public void Attribute_Damage(AG_Attribute attribute, float amount, C1_Creature instigator)
    {
        
    }
    
    public float Attribute_Get_Current(AG_Attribute attribute)
    {
        return 0f;
    }
    
    public float Attribute_Get_Max(AG_Attribute attribute)
    {
        return 0f;
    }
    
    public float Attribute_Get_Percent(AG_Attribute attribute)
    {
        return 0f;
    }
        
    // ---------------------------------------------------------------------------------
    // Equipment
    // ---------------------------------------------------------------------------------

    public Action<C1_Creature, ImpAsset, ImpAsset> on_equip;
    public Action<C1_Creature, ImpAsset, ImpAsset> on_unequip;
    
    public void Equipment_Equip(ImpAsset slot, ImpAsset item)
    {
        Equipment_Unequip(slot);
        config.equipment[slot] = item;
        on_equip.Invoke( this, slot, item);
    }
    
    public void Equipment_Unequip(ImpAsset slot)
    {
        ImpAsset? uneqipped = Equipment_Get(slot);
        config.equipment.Remove(slot);
        if (uneqipped != null)
        {
            on_unequip.Invoke(this, slot, uneqipped);
        }
    }

    public ImpAsset Equipment_Get(ImpAsset slot)
    {
        return config.equipment.TryGetValue(slot, out ImpAsset item) ? item : null;
    }
    
        
    // ---------------------------------------------------------------------------------
    // Inventory
    // ---------------------------------------------------------------------------------
    public Action<C1_Creature, ImpAsset, int> on_inventory_change;

    public void Inventory_Add(ImpAsset item, int amount)
    {
        int _init_amount = Inventory_GetAmount(item);
        _init_amount += amount;
        config.inventory[item] = _init_amount;
    }
    
    public Dictionary<ImpAsset,int> Inventory_Get(int minimum=1)
    {
        Dictionary<ImpAsset, int> output = new();

        foreach (var (item, amount) in config.inventory)
        {
            if (amount >= minimum)
            {
                output[item] = amount;
            }
        }
        return output;
    }
    
    public int Inventory_GetAmount(ImpAsset item)
    {
        return config.inventory.TryGetValue(item, out int amount) ? amount : 0;
    }
    
    // ---------------------------------------------------------------------------------
    // Leveling
    // ---------------------------------------------------------------------------------
    Action<C1_Creature, AG_Leveling, float> on_leveling_xp_change;
    Action<C1_Creature, AG_Leveling, int> on_leveling_rank_change;
    
    public void Leveling_XP_Add(AG_Leveling level, float amount)
    {
        config.leveling[level] += amount;
    }
    public float Leveling_XP_Get(AG_Leveling level)
    {
        return config.leveling.TryGetValue(level, out float xp) ? xp : 0f;
    }
    
}


public class A_CreatureConfig : ImpAsset
{
    public Dictionary<AG_Attribute, float> current_attributes = new();
    public Dictionary<ImpAsset,ImpAsset> equipment = new();
    public Dictionary<ImpAsset,int> inventory = new();
    public Dictionary<AG_Leveling,float> leveling = new();
}