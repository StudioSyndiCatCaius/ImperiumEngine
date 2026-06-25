using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;

namespace ImperiumEngine.Interfaces;

// Implemented by structs/types to customize how they appear in the property editor
// AND how they are serialized to/from TOML. Both hooks are optional.
public interface I_PropertyType
{
    // Emits rows into the property editor. ctx.Row adds a labelled value widget;
    // ctx.EmitDefault falls back to the standard reflected sub-field rows.
    void BuildPropertyEditor(PropertyEditorContext ctx) => ctx.EmitDefault();

    // Return a TOML-native value (string, bool, long, double, TomlArray, TomlTable …) to
    // override how this value is written. Return null to use ImpSave's default behavior.
    object? Savable_ToToml() => null;

    // Given a raw TOML value, return the deserialized struct value.
    // Return null to let ImpSave handle it.
    object? Savable_FromToml(object raw) => null;
}

// Passed to I_PropertyType.BuildPropertyEditor.
// GetValue re-reads the live value each call; Commit writes it back.
// Row(label, widget) inserts a custom value row; EmitDefault falls back to reflection.
public sealed class PropertyEditorContext
{
    public required Func<object?>                  GetValue    { get; init; }
    public required Action<object?>                Commit      { get; init; }
    public required Action<string, ImpComponent2D> Row         { get; init; }
    public required Action                         EmitDefault { get; init; }
}
