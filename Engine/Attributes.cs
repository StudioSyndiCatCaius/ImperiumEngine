namespace ImperiumEngine;

//contains all c# attributes for field, function, struct, etc.

// 1. Define a custom parameter attribute
[AttributeUsage(AttributeTargets.Field)]
public class ExposedAttribute : Attribute { }
