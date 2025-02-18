namespace ImperiumEngine.Source.Libs;
using static ImpLib_Math;

public static class ImpLib_GUID
{
    public static FGuid GUID_New()
    {
        Int32 max = 999999999;
        FGuid output = new FGuid(Int_Random(0,max), Int_Random(0,max), Int_Random(0,max), Int_Random(0,max));
        return output;
    }
    
}