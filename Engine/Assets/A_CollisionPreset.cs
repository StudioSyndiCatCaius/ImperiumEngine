using Engine.Core;
using Engine.Enums;

namespace Engine.Assets;

[AssetColor(90, 180, 110)]
public class A_CollisionPreset : ImpAsset
{
    public static A_CollisionPreset PRESET_NONE = new();
    public static A_CollisionPreset PRESET_MESH = new()
    {
        object_channel = ECollisionChannel.World,
        response_to_channel = { [ECollisionChannel.Visibility]=ECollisionResponse.Ignore }
    };
    public static A_CollisionPreset PRESET_PAWN = new()
    {
        object_channel = ECollisionChannel.Pawn,
    };
    public static A_CollisionPreset PRESET_INTERACTABLE = new()
    {
        response_to_channel = { [ECollisionChannel.Cursor]=ECollisionResponse.Ignore }
    };
    
    [ImpVar] public ECollisionChannel object_channel = ECollisionChannel.World;
    [ImpVar] public Dictionary<ECollisionChannel,ECollisionResponse> response_to_channel=new();
}