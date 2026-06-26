using ImperiumEngine.Structs;

namespace ImperiumEngine.Interfaces;

public class I_General
{
    virtual public TText General_GetText() { return new TText();}
    virtual public TText General_GetDescription() { return new TText();}
}