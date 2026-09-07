using System.Numerics;
using Engine.Comps._1D;
using Engine.Core;
using Engine.Structs;

namespace Engine.Assets;

public enum ESaveDir
{
    User, //saves to the computer's user folder 
    Game, //saves to the game folder
}

[Title("Save")]
public abstract class A_Save : ImpAsset
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar][Config] public static ESaveDir save_dir;
    [ImpVar][Config] public static TClass<A_Save_Game> save_game_class;
    [ImpVar][Config] public static string save_game_prefex="save_";
    [ImpVar][Config] public static TClass<A_Save_Global> save_global_class;
    [ImpVar][Config] public static string save_global_name="global";

    public static string GetCurrentSaveDir()
    {
        switch (save_dir)
        {
            case ESaveDir.User: return "";
            case ESaveDir.Game: return "";
        }

        return "";
    }

    // ---------------------------------------------------------------------------------
    // GAME
    // ---------------------------------------------------------------------------------

    public static A_Save GetCurrentSave(bool global)
    {
        if(global) return App.save_global;
        return App.save_game;
    }
    
    //saves the current game to the given slot
    public static bool Game_WriteToSlot(int slot)
    {
        return false;
    }
    
    public static bool Game_WriteToPath(string path)
    {
        return false;
    }

    public A_Save_Game Game_LoadFromSlot(int slot)
    {
        return null;
    }
    public A_Save_Game Game_LoadFromPath(string path)
    {
        return null;
    }

    public void Game_Start(A_Save_Game save, bool load_scene)
    {
        
    }
    
    // ---------------------------------------------------------------------------------
    // VARS
    // ---------------------------------------------------------------------------------

    public static void vSet_Bool(TLabel label, bool value, bool global = false) { GetCurrentSave(global).vars_bool.Add(label, value); }
    public static bool vGet_Bool(TLabel label, bool global = false) { return GetCurrentSave(global).vars_bool.TryGetValue(label, out bool value) ? value : false; }
    public static void vSet_Int(TLabel label, int value, bool global = false) { GetCurrentSave(global).vars_int.Add(label, value); }
    public static int vGet_Int(TLabel label, bool global = false) { return GetCurrentSave(global).vars_int.TryGetValue(label, out int value) ? value : 0; }
    public static void vSet_Float(TLabel label, float value, bool global = false) { GetCurrentSave(global).vars_float.Add(label, value); }
    public static float vGet_Float(TLabel label, bool global = false) { return GetCurrentSave(global).vars_float.TryGetValue(label, out float value) ? value : 0f; }
    public static void vSet_String(TLabel label, string value, bool global = false) { GetCurrentSave(global).vars_string.Add(label, value); }
    public static string vGet_String(TLabel label, bool global = false) { return GetCurrentSave(global).vars_string.TryGetValue(label, out string value) ? value : ""; }
    public static void vSet_Vector(TLabel label, Vector3 value, bool global = false) { GetCurrentSave(global).vars_vectors.Add(label, value); }
    public static Vector3 vGet_Vector(TLabel label, bool global = false) { return GetCurrentSave(global).vars_vectors.TryGetValue(label, out Vector3 value) ? value : new Vector3(); }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    [ImpVar] public Dictionary<TLabel,bool> vars_bool=new();
    [ImpVar] public Dictionary<TLabel,int> vars_int=new();
    [ImpVar] public Dictionary<TLabel,float> vars_float=new();
    [ImpVar] public Dictionary<TLabel,string> vars_string=new();
    [ImpVar] public Dictionary<TLabel,Vector3> vars_vectors=new();
    
    [ImpVar] public Dictionary<TLabel,A_CreatureData> creatures=new();
    
    public string savepath; //the path to the save file1
}

public class A_Save_Game : A_Save
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static A_Save_Game Load_FromSlot(int slot)
    {
        return null;
    }
    public static A_Save_Game Load_FromPath(string path)
    {
        return null;
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar] public TRef<A_Scene> scene; //path to current scene the player is on
    [ImpVar] public List<TTransform3> player_positions;
    
    
    public bool Save_ToSlot(int slot)
    {
        return false;
    }

    public bool Save_ToPath(string path)
    {
        return false;
    }
}

public class A_Save_Global : A_Save
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static A_Save_Global Get()
    {
        if(App.save_global==null) App.save_global = new A_Save_Global();
        
        return App.save_global;
    }
    public static A_Save_Global Load()
    {
        return null;
    }
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
}