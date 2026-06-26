using ImperiumEngine.Structs;

namespace ImperiumCore.Assets.Gameplay;

public enum EFactionAffinity
{
    Friendly,
    Hostile,
    Neutral,
}

public class A_Faction : A_GameplayAsset
{
    public TTag FactionTag;
    public Dictionary<TTag,EFactionAffinity> FactionAffinities;
}