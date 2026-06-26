using ImperiumEngine.Structs;

namespace ImperiumCore.Structs;

public class TGlobalVars
{
    [ImpVar] public TTagSet tags;
    [ImpVar] public Dictionary<string, bool> bools;
    [ImpVar] public Dictionary<string, float> floats;
    [ImpVar] public Dictionary<string, int> ints;
    [ImpVar] public Dictionary<string, string> strings;
}