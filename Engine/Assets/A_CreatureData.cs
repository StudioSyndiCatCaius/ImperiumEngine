using ImperiumEngine.Assets.General;
using ImperiumEngine;

namespace ImperiumEngine.Assets;

public class A_CreatureData : ImpAsset
{
    [ImpVar] public Dictionary<AG_Attribute, float> current_attribute_values = new();
    [ImpVar] public Dictionary<ImpAsset,int> inventory = new();
    [ImpVar] public Dictionary<ImpAsset,ImpAsset> equipment = new();
    
    
}