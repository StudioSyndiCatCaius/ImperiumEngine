using Engine.Comps._1D;
using Engine.Comps._3D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Assets;

[Title("Game Mode")]
public class A_GameMode : ImpAsset
{
    [ImpVar][Config] public static A_GameMode default_gamemode;
    
    public virtual TClass<C1_State>? GetState_Default() { return null; }
    public virtual TClass<C1_State>? GetState_Fallback() { return null; }

    public virtual void OnStart(A_Scene scene, A_GameMode instance) { }
    public virtual void OnEnd(A_Scene scene, A_GameMode instance) { }
    
    public virtual void OnPlayerStart(ImpPlayer player, TTransform3 start, A_Scene scene, A_GameMode instance) { }
}