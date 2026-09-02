using Engine.Interfaces;
using Engine.Structs;

namespace Engine.Assets.Flow;

public enum EQuestState
{
    Unstarted, Active, Finished,
}

public struct TQuestState
{
    public EQuestState state;
    public TTagSet tags;
    public List<Guid> active_states;
}

public class Flow_Quest : A_Flow, I_General
{
    [ImpVar][Category("General")] public TText title;
    [ImpVar][Category("General")] public A_Texture icon;
    [ImpVar][Category("General")] public TText description;
    [ImpVar][Category("General")] public TTagSet tags = new();
    
    [ImpVar][Category("Quest")] public bool is_repeatable;
    

}
