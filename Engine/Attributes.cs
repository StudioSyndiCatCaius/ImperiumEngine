namespace ImperiumEngine;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ImpVarAttribute : Attribute
{
    // Optional: allow a custom JSON key
    public string? Name { get; }

    public ImpVarAttribute(string? name = null) => Name = name;
}


[AttributeUsage(AttributeTargets.Field)]
public class ExportAttribute : Attribute
{
    
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ImpConfigAttribute : Attribute
{
    public string? Name { get; }
    public ImpConfigAttribute(string? name = null) => Name = name;
}