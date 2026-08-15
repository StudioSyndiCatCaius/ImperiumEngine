namespace ImperiumEngine;


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ImpVarAttribute : Attribute
{
    // Optional: allow a custom JSON key
    public string? Name { get; }

    public bool ReadOnly { get; init; }


    public bool Advanced { get; init; }
    
    public float Min { get; init; }
    public float Max { get; init; }

    public ImpVarAttribute(string? name = null) => Name = name;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class CategoryAttribute : Attribute
{
    //sorts into a custom editor category. if none given, the category is the name of the owning class
    public string? Name { get; }

    public CategoryAttribute(string name) => Name = name;
}

// overrides display name in the editor
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Enum)]
public sealed class TitleAttribute : Attribute
{
    
    public string? Name { get; }

    public TitleAttribute(string name) => Name = name;
}




[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AssetColorAttribute : Attribute
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    public AssetColorAttribute(byte r, byte g, byte b) { R = r; G = g; B = b; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ImpConfigAttribute : Attribute
{
    public string? Name { get; }
    public ImpConfigAttribute(string? name = null) => Name = name;
}


[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ImpClassAttribute : Attribute
{

    
    public bool Hidden { get; init; } //hides from the editor

    public bool Common { get; init; }  // for ImpComps. appears in the "Comps" tab next to the scene tree
}


// ========================================================================================================
// Methods
// =======================================================================================================

//in the editor, displays a button to call this function
[AttributeUsage(AttributeTargets.Method)]
public class CallInEditorAttribute : Attribute
{

}

// ========================================================================================================
// PULSE Visual Scripting
// ========================================================================================================

// indicates that this function is callable in the editor scripting system "Pulse"
[AttributeUsage(AttributeTargets.Method)]
public class PulseCallAttribute : Attribute
{
    
}

// only for "virtual" functions. indicates that this function is overridable in the editor scripting system "Pulse";
[AttributeUsage(AttributeTargets.Method)]
public class PulseOverrideAttribute : Attribute
{
    
}

