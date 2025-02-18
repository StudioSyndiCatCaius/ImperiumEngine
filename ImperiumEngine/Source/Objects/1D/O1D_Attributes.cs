namespace ImperiumEngine.Source.Objects._1D;

public class O1D_Attributes : ImpObject
{
    public Int32 AttributeLevel;
    private Dictionary<IA_Attribute, float> values_current;

    public float GetAttributeValue_Current(IA_Attribute attribute)
    {
        return values_current[attribute];
    }
    
    public float GetAttributeValue_Max(IA_Attribute attribute)
    {
        return attribute.Value_Max;
    }
    
    public void DamageAttribute(IA_Attribute attribute, float amount)
    {
        values_current[attribute] = float.Clamp(GetAttributeValue_Current(attribute) + amount,0,attribute.Value_Max);
    }
}

public class IA_Attribute : ImpAsset
{
    public FText DisplayName = new FText();
    public FText DisplayDescription = new FText();

    public float Value_Max;
}