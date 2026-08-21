using ImperiumEngine.Assets.Flow;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Structs;

namespace ImperiumEngine;

public class A_Save : ImpAsset
{
    [ImpVar] public Dictionary<TLabel,A_CreatureConfig> creatures=new()
    {
        ["_"]=new()
    };
    [ImpVar] public Dictionary<TLabel,A_SquadConfig> squads=new()
    {
        ["_"]=new()
    };
    
    [ImpVar] public Dictionary<Flow_Quest,TQuestState> quest_data=new();
}

public class Save_Game : A_Save
{
    public TRef<ImpScene> scene_current;
    public TTransform3 scene_position;
}


public class Save_Global : A_Save
{
    
}