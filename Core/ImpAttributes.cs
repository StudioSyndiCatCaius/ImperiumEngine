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
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class TitleAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}


// --------------------------------------------------------------------------------------------------------------------
// Property
// --------------------------------------------------------------------------------------------------------------------

/// <summary>
/// Marks a property as "advanced" — shown in a collapsed Advanced sub-section
/// at the bottom of its category in the property editor.
/// </summary>

[Flags]
public enum Pulse
{
    None  = 0,
    Read  = 1 << 0,   // 1
    Write = 1 << 1,   // 2
    BindSet = 1 << 2, // 4
    // Add more as needed
}


[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AdvancedDisplayAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property | 
                AttributeTargets.Field | 
                AttributeTargets.Parameter, 
    AllowMultiple = false, 
    Inherited = true)]
public class PulseAttribute : Attribute
{
    public Pulse Permissions { get; }

    public PulseAttribute(Pulse permissions)
    {
        Permissions = permissions;
    }
}

// --------------------------------------------------------------------------------------------------------------------
// Function
// --------------------------------------------------------------------------------------------------------------------



// --------------------------------------------------------------------------------------------------------------------
// Struct
// --------------------------------------------------------------------------------------------------------------------


// --------------------------------------------------------------------------------------------------------------------
// Class
// --------------------------------------------------------------------------------------------------------------------


//marks this as having a Pulse Dictionary of global constants (as a TClass)
[AttributeUsage( AttributeTargets.Class )]
public sealed class ConstClassAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

//marks this as having a Pulse Dictionary of global constants (as an ImpAsset)
[AttributeUsage( AttributeTargets.Class )]
public sealed class ConstAssetAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}