using ImperiumEngine.Assets.General;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

// Creature is an advanced component for handling common gameplay functions for an entity (E.G, Attribute, Abilities, Equipment, Inventory, etc.)  
public class C1_Creature : ImpComp
{
    [ImpVar] public Imp3D creature_root; // intended to be the rootmost comp of this scene.
    
    private List<C1_Aura> _auras;
    private List<C1_Ability> _abilities;
    public List<Object> _modifiers;
    public A_CreatureConfig config;
    public AG_Faction faction;


    public override void OnBegin()
    {
        base.OnBegin();
        if (config == null) { config = new (); }
    }

    // ---------------------------------------------------------------------------------
    // Modifiers
    // ---------------------------------------------------------------------------------
    public void Modifier_Register(Object modifier, bool registered)
    {
        if(registered && !_modifiers.Contains(modifier) && modifier is I_Creature)
        {
            _modifiers.Add(modifier);
        }
        else if(!registered && _modifiers.Contains(modifier))
        {
            _modifiers.Remove(modifier);
        }
    }

    public List<Object> Modifiers_GetAll()
    {
        List<Object> output=_modifiers;
        output.Append(_abilities);
        output.Append(_auras);
        output.Append(Equipment_GetList());
        return output;
    }
    
    // ---------------------------------------------------------------------------------
    // Abilities
    // ---------------------------------------------------------------------------------
    
    //abilities granted OnBegin
    [ImpVar][Category("Abilities")] public List<TClass<C1_Ability>> auto_abilities;

    public void Ability_SetGranted(TClass<C1_Ability> ability, bool granted)
    {
        if (Ability_IsGranted(ability) != granted)
        {
            if (granted)
            {
                C1_Ability ab = null; // code to make ability
                _abilities.Add(ab);
            }
            else
            {
                C1_Ability a = Ability_Get(ability);
                _abilities.Remove(a);
                a.Destroy();
            }
        }
    }
    
    public bool Ability_IsGranted(TClass<C1_Ability> ability)
    {
        foreach (var a in _abilities)
        {
            if (a.GetType() == ability.Get())
            {
                return true;
            }
        }
        return false;
    }
    
    public void Ability_Activate(TClass<C1_Ability> ability, object context)
    {
        C1_Ability a = Ability_Get(ability);
        if (a != null)
        {
            a.Ability_Activate(context);
        }
    }
    
    public C1_Ability Ability_Get(TClass<C1_Ability> ability)
    {
        foreach (var a in _abilities)
        {
            if (a.GetType() == ability.Get())
            {
                return a;
            }
        }
        return null;
    }
    
    public void Ability_Stop(TClass<C1_Ability> ability, bool cancelled)
    {
        C1_Ability a = Ability_Get(ability);
        if (a != null)
        {
            a.Ability_Stop(cancelled);
        }
    }
    
    // ---------------------------------------------------------------------------------
    // Attributes
    // ---------------------------------------------------------------------------------
    
    //self, attribute, amount, instigator
    public Action<C1_Creature, AG_Attribute, float, C1_Creature> on_attribute_damage;
    
    public void Attribute_Damage(AG_Attribute attribute, float amount, C1_Creature instigator,AG_DamageType damageType)
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

    public bool Attributes_HasMinimum(Dictionary<AG_Attribute, float> minimums)
    {
        foreach (var (attr, min) in minimums)
        {
            if(Attribute_Get_Current(attr) < min) { return false;}
        }
        return true;
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

    public List<ImpAsset> Equipment_GetList()
    {
        return config.equipment.Values.ToList();
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
    
    // ---------------------------------------------------------------------------------
    // Aura
    // ---------------------------------------------------------------------------------
    public C1_Aura Aura_Add(TClass<C1_Aura> aura, C1_Creature instigator, object context)
    {
        return null;
    }

    public void Aura_Remove(C1_Aura aura)
    {
        if (_auras.Contains(aura))
        {
            _auras.Remove(aura);
            aura.Destroy();
        }
    }
    
    public void Aura_Remove_OfClass(TClass<C1_Aura> aura, bool all=true)
    {
        foreach (var a in _auras)
        {
            if (a.GetType() == aura.Get())
            {
                Aura_Remove(a);
            }
        }
    }

    public void Aura_Remove_AllOfTag(TTagSet tags)
    {
        foreach (var a in _auras)
        {
            if (a.tags.HasAny(tags))
            {
                Aura_Remove(a);
            }
        }
    }
    
    public bool Aura_Has_OfTag(TTag tag)
    {
        foreach (var a in _auras)
        {
            if(a.tags.HasTag(tag)) { return true;}
        }
        return false;
    }
    
    public void Aura_Remove_All()
    {
        foreach (var a in _auras)
        {
            Aura_Remove(a);
        }
    }
    
    
    // ---------------------------------------------------------------------------------
    // Faction
    // ---------------------------------------------------------------------------------
    public EFactionAffinity Faction_GetAffinityTo(C1_Creature other)
    {
        if (faction != null && other.faction != null)
        {
            return faction.faction_affinity[other.faction.faction_tag];
        }
        return EFactionAffinity.Neutral;
    }
}


public class A_CreatureConfig : ImpAsset
{
    public Dictionary<AG_Attribute, float> current_attributes = new();
    public Dictionary<ImpAsset,ImpAsset> equipment = new();
    public Dictionary<ImpAsset,int> inventory = new();
    public Dictionary<AG_Leveling,float> leveling = new();
}


[ImpClass(Hidden = true)]
//an global instance of a creature by an ImpAsset as its Identity. E.G a party member in an RPG 
public class ImpCreature : ImpComp
{
    public C1_Creature creature;
    public ImpAsset identity;
}