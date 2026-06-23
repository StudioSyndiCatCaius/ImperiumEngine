namespace ImperiumCore;

//This class implements the varies attributes for all types, VARIABLES, STRUCTS, FUNCTIONS and maybe GENERIC

// --------------------------------------------------------------------------------------------------------------------
// COMMON
// --------------------------------------------------------------------------------------------------------------------

/// <summary>Makes the property visible and editable in any editor panel.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
public sealed class ExposedAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ImpVarAttribute : Attribute { }

/// <summary>
/// Optional. Groups this property under a named section inside its settings panel.
/// When omitted the property appears under "General".
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CategoryAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// Optional. The in-editor display name of the property.
/// When omitted the property uses the name of the field or property.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class TitleAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}


// --------------------------------------------------------------------------------------------------------------------
// Property
// --------------------------------------------------------------------------------------------------------------------



// --------------------------------------------------------------------------------------------------------------------
// Function
// --------------------------------------------------------------------------------------------------------------------



// --------------------------------------------------------------------------------------------------------------------
// Struct
// --------------------------------------------------------------------------------------------------------------------