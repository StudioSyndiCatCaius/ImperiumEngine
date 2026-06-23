using ImperiumCore.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects._2D;

public struct TMenuBarOpt
{
    public TText text;
    public List<TMenuBarOpt> suboptions;
    
    public Action<TMenuBarOpt> OnSelect;
}


public class O2D_MenuBar : ImpComponent2D
{
    public List<TMenuBarOpt> options;
}