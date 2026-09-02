using Engine.Core;
using Engine.Globals;
using Engine.Interfaces;

namespace Engine.Structs;

/*
 * stores a file path. LONG TERM, consider having this use some sort of byte index or hash map, where instead of a full string,
 * we store some sort of reference to folders that themselves are stored in some kind of map (for performance)
 */
public struct TFile : I_Property
{

    [ImpVar] public string path;

    public TFile(string? path) { this.path = path ?? ""; }

    public string Get() { return GFile.Make_Path_Absolute(path); }
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
