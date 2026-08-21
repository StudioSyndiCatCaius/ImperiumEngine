using ImperiumEngine.Enums;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Assets.General;

public class AG_Faction : A_General
{
    [ImpVar] public TTag faction_tag;
    [ImpVar] public Dictionary<TTag,EFactionAffinity> faction_affinity;
}