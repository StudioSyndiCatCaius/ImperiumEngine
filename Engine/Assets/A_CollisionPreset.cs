using ImperiumEngine.Enums;

namespace ImperiumEngine.Assets;

[AssetColor(90, 180, 110)]
public class A_CollisionPreset : ImpAsset
{
    public static A_CollisionPreset PRESET_NONE = new();
    public static A_CollisionPreset PRESET_MESH = new()
    {
        response_to_channel = { [ECollisionChannel.Visibility]=ECollisionResponse.Ignore }
    };
    public static A_CollisionPreset PRESET_INTERACTABLE = new()
    {
        response_to_channel = { [ECollisionChannel.Cursor]=ECollisionResponse.Ignore }
    };
    
    [ImpVar] public Dictionary<ECollisionChannel,ECollisionResponse> response_to_channel=new();
}