using Engine.Comps._1D;
using Engine.Enums;
using Engine.Structs;

namespace Engine.Assets.General;

public class AG_TargetFilterPreset : A_General
{
    [ImpVar] public List<A_TargetFilter> filters;

    public List<C1_Creature> FilterCreatures(C1_Creature insigator, List<C1_Creature> creatures)
    {
        List<C1_Creature> output=new ();

        foreach (var creature in creatures)
        {
            foreach (var filter in filters)
            {
                if (filter.TargetIsValid(creature, insigator))
                {
                    output.Add(creature);
                }
            }
        }
        return output;
    }
    
    // ====================================================================================
    // STATIC
    // ====================================================================================

    public AG_TargetFilterPreset TARGET_FRIENDLY = new()
    {
        filters = new ()
        {
            new TargetFilter_Affinity { accepted_affinities = { EFactionAffinity.Friendly } }
        }
    };
    public AG_TargetFilterPreset TARGET_HOSTILE = new()
    {
        filters = new ()
        {
            new TargetFilter_Affinity { accepted_affinities = { EFactionAffinity.Hostile } }
        }
    };

    public AG_TargetFilterPreset TARGET_ALL = new();
    public AG_TargetFilterPreset TARGET_SELF = new()
    {
        filters = new ()
        {
            new TargetFilter_Self() { }
        }
    };
    
}

public class A_TargetFilter
{
    [ImpVar] public bool invert;
    public virtual bool TargetIsValid(C1_Creature target, C1_Creature instigator)
    {
        return true;
    }
}

// ##########################################################################################
// Filters
// ##########################################################################################

public class TargetFilter_Affinity : A_TargetFilter
{
    public List<EFactionAffinity> accepted_affinities;

    public override bool TargetIsValid(C1_Creature target, C1_Creature instigator)
    {
        return accepted_affinities.Contains(instigator.Faction_GetAffinityTo(target));
    }
}

public class TargetFilter_Self : A_TargetFilter
{
    public override bool TargetIsValid(C1_Creature target, C1_Creature instigator)
    {
        return target == instigator;
    }
}