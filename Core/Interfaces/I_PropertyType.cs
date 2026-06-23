

namespace ImperiumEngine.Interfaces;

// Implemented by structs to customize how they appear in the property/details editor —
// Imperium's equivalent of Unreal's IPropertyTypeCustomization. A customization emits
// rows into the shared 2-column table (via ctx.Row), so it stays column-aligned with
// the rest of the inspector. If not implemented, the default child rows are emitted.
public interface I_PropertyType
{
    void BuildPropertyEditor(PropertyEditorContext ctx) => ctx.EmitDefault();
}

// Handed to a struct's customization. GetValue is live (re-read it on every edit) so
// editing one field never clobbers another; Row(name, editor) adds a value row; and
// EmitDefault() falls back to the standard child rows.
public sealed class PropertyEditorContext
{
    public required Func<object?>          GetValue    { get; init; }
    public required Action<object?>        Commit      { get; init; }
    //public required Action<string, Widget> Row         { get; init; }
    public required Action                 EmitDefault { get; init; }
}
