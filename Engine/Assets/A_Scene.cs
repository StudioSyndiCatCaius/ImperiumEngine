using Engine.Comps._1D;
using Engine.Comps._3D;
using Engine.Core;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;

namespace Engine.Assets;

public class A_Scene : ImpAsset
{
    // ==============================================================================================================
    // STATIC
    // ==============================================================================================================


    // ==============================================================================================================
    // CLASS
    // ==============================================================================================================
    [ImpVar] public A_Environment environment = A_Environment.DEFAULT;
    [ImpVar] public A_GameMode override_gamemode;
    
    public A_GameMode GameMode_GetAsset()
    {
        if (override_gamemode != null) return override_gamemode;
        return A_GameMode.default_gamemode;
    }

    public ImpComp root;
    public A_Script? script;
    public Dictionary<int, string> prefab_refs = new();
    public bool is_running = false;
    public ImpViewport? viewport; // null = App.viewport_main. not serialized.
    public A_GameMode gamemode_instance=null;

    // Type -> live comps in this scene. A C3_Mesh is stored under C3_Mesh, Imp3D, and ImpComp
    // so GetAllOfClass(base) is a hash lookup, not a tree walk.
    internal readonly Dictionary<Type, HashSet<ImpComp>> comps_by_type = new();

    internal void CompIndex_Reg(ImpComp c)
    {
        if (c == null) return;
        Type[] chain = ImpComp.TypeChain(c.GetType());
        for (int i = 0; i < chain.Length; i++)
        {
            if (!comps_by_type.TryGetValue(chain[i], out HashSet<ImpComp> set))
            {
                set = new HashSet<ImpComp>();
                comps_by_type[chain[i]] = set;
            }
            set.Add(c);
        }
    }

    internal void CompIndex_Unreg(ImpComp c)
    {
        if (c == null) return;
        Type[] chain = ImpComp.TypeChain(c.GetType());
        for (int i = 0; i < chain.Length; i++)
        {
            if (!comps_by_type.TryGetValue(chain[i], out HashSet<ImpComp> set)) continue;
            set.Remove(c);
            if (set.Count == 0) comps_by_type.Remove(chain[i]);
        }
    }

    internal HashSet<ImpComp> CompIndex_Of(Type t)
    {
        if (t == null) return null;
        comps_by_type.TryGetValue(t, out HashSet<ImpComp> set);
        return set;
    }

    public void Begin()
    {
        if (is_running) return;
        is_running = true;
        Refresh();
        if (root != null)
        {
            root.AssignScene(this);
            ImpSandbox.current?.Bind(root);
        }
        if (this != App.scene_persistent)
        {
            A_GameMode _gm = GameMode_GetAsset();
            if (_gm != null)
            {
                gamemode_instance = _gm.Clone() as A_GameMode ?? _gm;
                gamemode_instance.OnStart(this, gamemode_instance);
                foreach (var p in App.players) _PlayerSpawn(p);
            }
            ImpPlayer.on_player_connect += _PlayerSpawn;
        }
    }

    private void _PlayerSpawn(ImpPlayer p)
    {
        if (gamemode_instance == null) return;
        TTransform3 spawn_point = new();
        C3_PlayerStart match = null;
        C3_PlayerStart fallback = null;
        HashSet<ImpComp> starts = CompIndex_Of(typeof(C3_PlayerStart));
        if (starts != null)
        {
            foreach (ImpComp c in starts)
            {
                if (c is not C3_PlayerStart ps) continue;
                if (ps.player_id == p.id) match = ps;
                else if (ps.player_id == 0) fallback = ps;
                if (match != null && (fallback != null || p.id == 0)) break;
            }
        }
        if (match != null) spawn_point = match.global_transform;
        else if (fallback != null) spawn_point = fallback.global_transform;
        gamemode_instance.OnPlayerStart(p, spawn_point, this, gamemode_instance);
    }

    public void End()
    {
        if (!is_running) return;
        if (this != App.scene_persistent)
        {
            ImpPlayer.on_player_connect -= _PlayerSpawn;
            ImpPhysics.Clear();
        }
        if (gamemode_instance != null)
        {
            gamemode_instance.OnEnd(this, gamemode_instance);
            gamemode_instance = null;
        }
        if (root != null) StripRuntimeComps(root);
        is_running = false;
    }

    static void StripRuntimeComps(ImpComp c)
    {
        for (int i = c.children.Count - 1; i >= 0; i--)
        {
            ImpComp child = c.children[i];
            StripRuntimeComps(child);
            if (!child.is_runtime) continue;
            c.children.RemoveAt(i);
            child.parent = null;
            child.AssignScene(null, true);
        }
    }


    public void Refresh()
    {
        if (environment != null)
        {
            Console.WriteLine("Begin scene: " + filepath);
            environment.Refresh();
        }
    }
    
    public void ProcessNotify(ENotifyProcess notify, double dt)
    {
        if (root != null)
        {
            root.AssignScene(this);
            root.ProcessNotify(notify, dt);
        }
    }
    

    //creates a prefab instance of this scene to place in another scene
    public ImpComp Instantiate()
    {
        ImpComp inst = root != null
            ? Activator.CreateInstance(root.GetType()) as ImpComp ?? new()
            : new();
        inst.Prefab_Build(this);
        return inst;
    }

    public override bool CanSave()
    {
        // Runtime instances (Play / Game.exe) must never write spawned comps back to the authored file.
        if (is_running) return false;
        return base.CanSave();
    }

    protected override string File_GetExtension() { return "ImpScene"; }

    public int PrefabRef_Intern(A_Scene prefab)
    {
        string path = GFile.Make_Path_Local(prefab.filepath);
        string ext = GetFileExtension();
        if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^ext.Length];
        }
        //return id if it exists
        foreach (var pair in prefab_refs)
            if (pair.Value == path) return pair.Key;

        //otherwise generate a new id
        int id;
        do { id=Random.Shared.Next(1_000_000, 10_000_000); } 
        while (prefab_refs.ContainsKey(id));
        prefab_refs[id] = path;
        return id;
    }

    public A_Scene PrefabRef_Load(int id)
    {
        if(!prefab_refs.TryGetValue(id,out string path) || string.IsNullOrEmpty(path)) return null;
        return GAsset.Asset_Load<A_Scene>(path+GetFileExtension());
    }

    public override void From_Table(TTable tbl)
    {
        base.From_Table(tbl);
        prefab_refs.Clear();

        TTable refs = tbl.get_Table("prefab_refs");
        if (refs != null)
        {
            foreach (var pair in refs.data)
            {
                if (int.TryParse(pair.Key.ToString(), out int id))
                {
                    prefab_refs[id] = pair.Value as string ?? "";
                }
            }
        }

        TTable vars = tbl.get_Table("vars");
        TTable root_tbl = vars?.get_Table("root");
        if (root_tbl != null)
        {
            root = ImpComp.From_Table(root_tbl, this);
            if (root != null) root.scene = this;
        }

        TTable script_tbl = vars?.get_Table("script");
        if (script_tbl != null)
        {
            script = new A_Script { is_inlined = true };
            script.From_Table(script_tbl);
        }
    }

    public override TTable To_Table()
    {
        //return base.To_Table();
        prefab_refs.Clear();

        TTable vars = new();
        if(root != null) vars.Set("root",root.To_Table(this));
        if (script != null)
        {
            script.is_inlined = true;
            vars.Set("script", script.To_Table());
        }

        TTable refs = new();
        foreach (var pair in prefab_refs)
        {
            refs.Set(pair.Key.ToString(),pair.Value);
        }

        TTable tbl = new();
        tbl.Set("type","A_Scene");
        tbl.Set("prefab_refs",refs);
        tbl.Set("vars",vars);
        
        return tbl;
    }

    // --------------------------------------------------
    // Property
    // --------------------------------------------------
    
    
}