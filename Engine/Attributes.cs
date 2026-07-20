namespace ImperiumEngine;

//contains all c# attributes for field, function, struct, etc.

// Shows a field in the editor inspector
[AttributeUsage(AttributeTargets.Field)]
public class ExposedAttribute : Attribute { }

// Marks a field as part of the save state. Automatically read from / written to
// the [entity.params] TOML table when loading levels or save files.
// Supported types: float, int, bool, string, Vector2, Vector3, Color, enum
[AttributeUsage(AttributeTargets.Field)]
public class ImpVarAttribute : Attribute { }


// Custom attribute to hold a hex color code
[AttributeUsage(AttributeTargets.Field)]
public class ColorHexAttribute : Attribute
{
    public string HexValue { get; }
    public ColorHexAttribute(string hexValue) => HexValue = hexValue;
}
