using ImperiumCore.Classes;
using ImperiumEngine.Interfaces;
using ImperiumEngine.Structs;

namespace ImperiumCore.Structs;


public enum ECreatureIDType
{
    ImpAsset,
    Label,
}

public struct TCreatureID : I_PropertyType
{
    public ECreatureIDType type;
    public string id;
}



public struct TCreatureData
{
    [ImpVar] public TTagSet tags;
    [ImpVar] public Dictionary<ImpAsset, float> asset_float; //attributes
    [ImpVar] public Dictionary<ImpAsset, int> asset_int; // inventory
    [ImpVar] public Dictionary<ImpAsset, ImpAsset> asset_links; //equipment
}

public struct TCreatureSet
{
    [ImpVar] public Dictionary<ImpAsset, TCreatureData> asset_data;
    [ImpVar] public Dictionary<TLabel, TCreatureData> label_data;
    
}