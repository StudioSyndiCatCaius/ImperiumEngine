using System.Numerics;
using System.Text.Json.Serialization;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Interfaces;

namespace ImperiumEngine.Structs;


public struct TFile : I_Property
{

    [ImpVar] public string path;

    public TFile(string? path) { this.path = path ?? ""; }


}

public struct TDirectory : I_Property
{

    [ImpVar] public string path;

}
