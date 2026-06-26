using ImperiumCore.Classes;
using ImperiumEngine.Structs;

namespace ImperiumCore.Assets;

public class A_GameplayAsset : ImpAsset
{
    [ImpVar][Exposed][Category("General")]
    public string Title;
    [ImpVar][Exposed][Category("General")]
    public A_Texture2D Icon;
    [ImpVar][Exposed][Category("General")]
    public string Description;
    [ImpVar][Exposed][Category("General")]
    public TTagSet Tags;
}