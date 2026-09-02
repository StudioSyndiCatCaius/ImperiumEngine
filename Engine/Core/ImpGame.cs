using Engine.Assets;
using Engine.Comps._1D;
using Engine.Structs;

namespace Engine.Core;

[Title("Game")]
public class ImpGame
{
    public static ImpGame current;

    public static C1_GameMode game_mode;
    
    [Category("Game")][ImpVar][Config] public static TRef<A_Scene> starting_scene;
    //[Category("Game")][ImpVar][Config] public static TClass<C1_GameMode> default_game_mode = new(typeof(GM_Gameplay));
    
    //[Category("Save")][ImpVar][Config] public static TRef<Save_Game> save_game_type;
    [Category("Save")][ImpVar][Config] public static string save_game_prefex="save_";
    //[Category("Save")][ImpVar][Config] public static TRef<Save_Global> save_global_type;
    [Category("Save")][ImpVar][Config] public static string save_global_name="global";

}