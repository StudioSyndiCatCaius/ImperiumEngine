using ImperiumEngine.Structs;

namespace ImperiumEngine.Main;

public enum ESaveLocation : byte
{
    User, //saves to user folder (On windows that is "{user}/SavedGames/Imperium/{game_name})" 
    Game // saves to a "Save" folder inside the game directory
}

public class ImpSave
{
    // ====================================================================================
    // Statics
    // ====================================================================================
    public static ImpSave_Game game;
    public static ImpSave_Game global;
    public static ESaveLocation save_location=ESaveLocation.User;

    public static string GetDir_SaveLocation()
    {
        string out_dir="";
        switch (save_location)
        {
            case ESaveLocation.User:
                out_dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games/Imperium/"); 
                break;
            case ESaveLocation.Game:
                out_dir=Path.Combine(ImpPath.GetDir_Game(), "Save/");
                break;
        }
        
        return out_dir;
    }
    
    public static string GetDir_SaveSlot(int slot)
    {
        return Path.Combine(GetDir_SaveLocation(), "slot_"+slot.ToString("D")+"/");
    }
    
    public static ImpSave_Game Game_Load_FromPath(string path)
    {
        return null;
    }
    public static ImpSave_Game Game_Load_FromSlot(int slot)
    {
        return null;
    }
    
    public static bool Game_Save_ToPath(ImpSave_Game game, string path)
    {
        return false;
    }
    public static bool Game_Save_ToSlot(ImpSave_Game game, int slot)
    {
        
        return false;
    }
    
    // ====================================================================================
    // Class
    // ====================================================================================
    public Guid guid;
    public int seed;
    
    public DateTime time_created;
    public DateTime time_saved; // time last saved

    public Dictionary<TLabel, bool> vars_bool;
    public Dictionary<TLabel, int> vars_int;
    public Dictionary<TLabel, string> vars_string;
    
    public TRef<ImpScene> current_scene;
    public List<TTransform3> current_positions; //saved positions of the player pawn

    public ImpSave()
    {
        guid = Guid.NewGuid();
        seed = new Random().Next();
    }
}


public class ImpSave_Game : ImpSave
{
    
}

public class ImpSave_Global : ImpSave
{
    
}
