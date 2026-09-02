using Engine.Core;

namespace Engine.Assets;

public class A_TextTable : ImpAsset
{
    [ImpVar] public Dictionary<string,TText> strings;
}