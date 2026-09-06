using Engine.Core;
using Engine.Globals;
using Engine.Interfaces;

namespace Engine.Structs;

/*
 * stores a file path. LONG TERM, consider having this use some sort of byte index or hash map, where instead of a full string,
 * we store some sort of reference to folders that themselves are stored in some kind of map (for performance)
 */
public struct TFile : I_Property, IEquatable<TFile>
{

    [ImpVar] public string path;

    public TFile(string? path) { this.path = path ?? ""; }

    public string Get() { return GFile.Make_Path_Absolute(path); }

    public bool Equals(TFile other) =>
        string.Equals(path, other.path, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is TFile other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(path ?? "");

    public static bool operator ==(TFile a, TFile b) => a.Equals(b);
    public static bool operator !=(TFile a, TFile b) => !a.Equals(b);
}

public struct TDirectory : I_Property
{

    [ImpVar] public string path;

}


public struct TFileLabel
{
    public TLabel mod; // Example: {game}, {engine}, {mod1}, {mod2}
    public string path; // Example: txt_grass.png, /creature/slime.glb
    public TLabel ext;
}
