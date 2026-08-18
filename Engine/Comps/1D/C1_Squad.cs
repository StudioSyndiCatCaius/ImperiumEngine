using ImperiumEngine.Assets;

namespace ImperiumEngine.Comps._1D;

public struct TSquadMemberData
{
    public AG_SquadIdentity current_squad;
}

public class C1_Squad : ImpComp
{
    public A_SquadConfig config;

    public TSquadMemberData Member_GetSquadData(ImpAsset member)
    {
        return config.member_config[member];
    }
    
    public bool Member_IsInSquad(ImpAsset member, AG_SquadIdentity squad)
    {
        return Member_GetSquadData(member).current_squad==squad;
    }
    
    public void Member_SetInSquad(ImpAsset member, AG_SquadIdentity squad, bool in_squad)
    {
        bool _isin=Member_IsInSquad(member, squad);
        TSquadMemberData data = config.member_config[member];
        if (!_isin && in_squad)
        {
            data.current_squad = squad;
            config.member_config[member] = data;
        }
        else if (_isin && !in_squad)
        {
            data.current_squad = null;
            config.member_config[member] = data;
        }
    }

    public void Members_RemoveAll(AG_SquadIdentity squad)
    {
        
    }

    public void Members_SetInSquad(List<ImpAsset> members, AG_SquadIdentity squad, bool in_squad, bool clear_first=false)
    {
        if (clear_first)
        {
            Members_RemoveAll(squad);
        }
        foreach (ImpAsset member in members)
        {
            Member_SetInSquad(member, squad, in_squad);
        }
    }

    public void Formation_Swap(ImpAsset A, ImpAsset B)
    {
        
    }
    
    public void Formation_SwapIndecies(int A, int B)
    {
        
    }
}

public class A_SquadConfig : ImpAsset
{
    public AG_SquadIdentity current_squad;
    public List<ImpAsset> formation;
    public Dictionary<ImpAsset,TSquadMemberData> member_config;

}

public class AG_SquadIdentity : A_General
{
    // ====================================================================================
    // Class
    // ====================================================================================
    
    // ====================================================================================
    // Static
    // ====================================================================================
    public static AG_SquadIdentity PARTY_ACTIVE = new();
    public static AG_SquadIdentity PARTY_RESERVE = new();
}