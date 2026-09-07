using Engine.Assets;
using Engine.Comps._1D;
using Engine.Structs;

namespace Engine.Core;

[Title("Game")]
public class ImpGame
{
    public static ImpGame current;
    
    [Category("Game")][ImpVar][Config] public static TRef<A_Scene> starting_scene;
    //[Category("Game")][ImpVar][Config] public static TClass<C1_GameMode> default_game_mode = new(typeof(GM_Gameplay));
    
    

}