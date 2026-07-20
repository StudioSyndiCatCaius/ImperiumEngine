using Tomlyn.Model;

namespace ImperiumEngine.Interfaces;

// Implemented by a struct or class that needs custom TOML (de)serialization instead of the
// default "cascade every [ImpVar] field" behaviour. ImpToml checks for this interface first,
// so a type opts into bespoke logic (compact formats, omitting identity values, versioning...)
// simply by implementing it.
//
// The passed table is the type's own sub-table: write your keys into it, and read them back
// from it. For structs, ImpToml boxes the value before calling File_ReadFrom, so mutations to
// `this` are preserved.
public interface I_Serialize
{
    // Write this value's state into `table`.
    void File_WriteTo(TomlTable table);

    // Read this value's state from `table` (missing keys should keep their default).
    void File_ReadFrom(TomlTable table);
}
