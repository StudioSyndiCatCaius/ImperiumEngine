namespace ImperiumEngine.Source.Interfaces;

public interface ICommonAsset
{
    virtual public FText GetDisplayName()
    {
        return new FText();
    }

    virtual public FText GetDisplayDescription() { return new FText(); }
}