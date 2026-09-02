using Engine.Enums;
using Engine.Structs;

namespace Engine.Assets.General;

public class AG_Faction : A_General
{
    [ImpVar] public TTag faction_tag;
    [ImpVar] public Dictionary<TTag,EFactionAffinity> faction_affinity;
}