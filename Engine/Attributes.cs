

namespace Engine;


[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ImpVarAttribute : Attribute
{
    // Optional: allow a custom JSON key
    public string? Name { get; }
    
    public bool ReadOnly { get; init; } //DEPRC
    public bool Hidden { get; init; }

    public bool Advanced { get; init; }
    
    public float Min { get; init; }
    public float Max { get; init; }

    public ImpVarAttribute(string? name = null) => Name = name;
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class CategoryAttribute : Attribute
{
    //sorts into a custom editor category. if none given, the category is the name of the owning class
    public string? Name { get; }

    public CategoryAttribute(string name) => Name = name;
}

// overrides display name in the editor
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Enum | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TitleAttribute : Attribute
{
    
    public string? Name { get; }

    public TitleAttribute(string name) => Name = name;
}


//indicates a ImpAsset is "built-in", meaning it can be select in editor DLG_PickAsset under the "built-ins" section.  
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class BuiltinAttribute : Attribute
{
    // Optional: allow a custom JSON key
    public string? Name { get; }

    public BuiltinAttribute(string? name = null) => Name = name;
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
public sealed class ConfigAttribute : Attribute
{
    public string? Name { get; }
    public ConfigAttribute(string? name = null) => Name = name;
}


[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ImpClassAttribute : Attribute
{
    public bool GlobalizeFunctions { get; init; } //Mainly for scripting like .lua. TRUE= all `[ScriptCall] public static` functions() are global, like "MyFunc(35)". FALSE= you must call class name first, like "ImpWhatever.MyFunc(35)" 
    public bool Hidden { get; init; } //hides from the editor

    public bool Common { get; init; }  // for ImpComps. appears in the "Comps" tab next to the scene tree
}


// ========================================================================================================
// Methods
// =======================================================================================================

//in the editor, displays a button to call this function in its owning object's inspector
[AttributeUsage(AttributeTargets.Method)]
public class CallInEditorAttribute : Attribute
{

}

// ========================================================================================================
// Scripting
// ========================================================================================================

// Imperium supports 2 forms of scripting: Lua, and a cutsom visual scripting system called "Pulse"

// indicates that this function is callable in the scripting system
[AttributeUsage(AttributeTargets.Method)]
public class ScriptCallAttribute : Attribute
{
    //public bool pure { get; set; } //only for non-void return functions. defaults to pure with 
}

// only for "virtual" functions. indicates that this function is overridable in the scripting system 
[AttributeUsage(AttributeTargets.Method)]
public class ScriptHookAttribute : Attribute
{
    
}

