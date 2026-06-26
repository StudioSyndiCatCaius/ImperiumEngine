using ImperiumCore;
using ImperiumCore.Assets.Gameplay;
using ImperiumCore.Classes;
using ImperiumCore.Structs;

namespace ImperiumEngine.Objects._1D;

//an entity with complex data like attributes, equipment, inventory, & skills, and can be written directly to and from the save game
public class O1D_Creature : ImpComponent
{
    
    public O1D_Creature()
    {
        
    }

    [ImpVar] public TCreatureData data;
    [ImpVar] public bool UseSaveID=false;
    [ImpVar] public TCreatureID saveID;
    
    public TCreatureData GetCreatureData() => data;
    
    // ------------------------------------------------------------------------------------------------
    // Faction
    // ------------------------------------------------------------------------------------------------
    
    public A_Faction Faction;

    EFactionAffinity Faction_GetAffinityTo(O1D_Creature other)
    {
        return Faction.FactionAffinities[other.Faction.FactionTag];
    }

    List<O1D_Creature> Faction_FilterByAffinity(List<O1D_Creature> creatures, EFactionAffinity affinity)
    {
        List<O1D_Creature> cs = null;
        foreach (var _c in creatures)
        {
            if (Faction_GetAffinityTo(_c) == affinity)
            {
                cs.Add(_c);
            }
        }
        return cs;
    }
    
    // ------------------------------------------------------------------------------------------------
    // Targets
    // ------------------------------------------------------------------------------------------------
    
    public O1D_Creature target_active;
    public List<O1D_Creature> target_list;
    
    
}