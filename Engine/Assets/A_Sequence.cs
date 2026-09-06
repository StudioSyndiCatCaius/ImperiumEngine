using Engine.Core;

namespace Engine.Assets;

public struct TSequenceTrack
{
    public Type type;
    public Dictionary<float, object> keys;
}

/*
 *  a fully custom animated sequence like in UE
 */
public class A_Sequence : ImpAsset
{
    public Type root_type;
}