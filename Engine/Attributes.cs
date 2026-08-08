namespace ImperiumEngine;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ImpVarAttribute : Attribute
{
    // Optional: allow a custom JSON key
    public string? Name { get; }

    public ImpVarAttribute(string? name = null) => Name = name;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class CategoryAttribute : Attribute
{
    //sorts into a custom editor category. if none given, the category is the name of the owning class
    public string? Name { get; }

    public CategoryAttribute(string name) => Name = name;
}


// indicates that this function is callable in the editor scripting system "Pulse"
[AttributeUsage(AttributeTargets.Method)]
public class ImpFuncAttribute : Attribute
{
    
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