using ImperiumEngine.Comps._1D;
using ImperiumEngine.Structs;

namespace ImperiumEngine;

// One running game. C# statics cannot be instanced — game-scoped state lives here
// instead. Get(0) is the editor / standalone host. Get(1) is Play-in-Editor.
// `current` is the ambient game for the tick in progress; ImpScene.current follows it.
// Not A_Game — that asset is the project (.ImpGame). This is the live session.
public class ImpGame
{
    // #################################################################################
    // Static
    // #################################################################################
    
    [Category("Game")][ImpVar][Config] public static TRef<ImpScene> starting_scene;
    [Category("Game")][ImpVar][Config] public static TClass<C1_GameMode> default_game_mode;
    
    [Category("Save")][ImpVar][Config] public static TRef<Save_Game> save_game_type;
    [Category("Save")][ImpVar][Config] public static string save_game_prefex="save_";
    [Category("Save")][ImpVar][Config] public static TRef<Save_Global> save_global_type;
    [Category("Save")][ImpVar][Config] public static string save_global_name="global";
    
    public const int ID_HOST = 0;
    public const int ID_PLAY = 1;

    static readonly List<ImpGame> _games = new();
    public static ImpGame current;

    public C1_GameMode game_mode;
    public ImpPhys phys;

    public ImpPhys Phys_Get()
    {
        if (phys == null)
        {
            phys = new ImpPhys();
            phys.Create();
        }
        return phys;
    }

    public void Phys_Dispose()
    {
        if (phys == null)
        {
            return;
        }
        phys.Dispose();
        phys = null;
    }

    public static ImpGame Get(int id = 0)
    {
        if (id < 0)
        {
            return null;
        }
        if (id == ID_HOST)
        {
            EnsureHost();
        }
        if (id >= _games.Count)
        {
            return null;
        }
        return _games[id];
    }

    public static void EnsureHost()
    {
        ImpGame g = Register(ID_HOST);
        if (g.scene == null)
        {
            g.scene = ImpScene.BootScene();
        }
        if (current == null)
        {
            current = g;
        }
    }

    public static ImpGame Bind(ImpGame game)
    {
        ImpGame prev = current;
        current = game;
        return prev;
    }

    public static ImpGame Play_Start(ImpScene source)
    {
        Play_Stop();
        if (source == null)
        {
            return null;
        }
        ImpGame game = Register(ID_PLAY);
        game.scene = source.PlayCopy();
        return game;
    }

    public static void Play_Stop()
    {
        ImpGame dying = Get(ID_PLAY);
        if (dying == null)
        {
            return;
        }
        // Hand input back to the editor. Authoritative — covers every stop path, not just the
        // toolbar. Leaving a player pointed at a dead session locks the user out of both.
        for (int i = 0; i < ImpPlayer.players.Count; i++)
        {
            if (ImpPlayer.players[i].target_game == dying)
            {
                ImpPlayer.players[i].target_game = null;
            }
        }
        if (current == dying)
        {
            current = Get(ID_HOST);
        }
        ImpScene s = dying.scene;
        if (s != null)
        {
            ImpGame prev = Bind(dying);
            if (s.is_running)
            {
                s.REnd();
                s.is_running = false;
            }
            if (s.root != null)
            {
                s.root.Destroy();
            }
            dying.Phys_Dispose();
            Bind(prev ?? Get(ID_HOST));
        }
        _games[ID_PLAY] = null;
    }

    static ImpGame Register(int id)
    {
        while (_games.Count <= id)
        {
            _games.Add(null);
        }
        if (_games[id] == null)
        {
            ImpGame g = new ImpGame();
            g.id = id;
            _games[id] = g;
        }
        return _games[id];
    }

    // #################################################################################
    // Class
    // #################################################################################

    public int id;

    // ------------------------------------------
    // Scene
    // ------------------------------------------
    public ImpScene scene_previous; // when transit between scenes this is the previous root scene
    
    ImpScene _scene;
    public ImpScene scene
    {
        get => _scene;
        set
        {
            if (_scene == value)
            {
                if (value != null)
                {
                    value.game = this;
                }
                return;
            }
            if (_scene != null && _scene.game == this)
            {
                _scene.game = null;
            }
            _scene = value;
            if (_scene != null)
            {
                _scene.game = this;
            }
        }
    }
    
    // ------------------------------------------
    // Save
    // ------------------------------------------
    public Save_Game save_game;
    public Save_Global save_global;
    
    
    public void SaveGame_Write_ToFile(string path)
    {
        
    }

    public void SaveGame_Write_ToSlot(int slot)
    {
        
    }
    
    // ------------------------------------------
    // Creature
    // ------------------------------------------
    
    
    // ------------------------------------------
    // Squad
    // ------------------------------------------
}
