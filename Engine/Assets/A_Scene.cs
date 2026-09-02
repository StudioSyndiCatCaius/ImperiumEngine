using Engine.Core;
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
    [ImpVar] public A_Environment environment=new();
    
    
    public ImpComp root;
    public Dictionary<int, string> prefab_refs = new();
    public bool is_runtime=false;
    public ImpViewport? viewport; // null = App.viewport_main. not serialized.

    public void Begin()
    {
        Refresh();
    }
    
    public void End()
    {
        
    }
    
    
    public void Refresh()
    {
        if (environment != null)
        {
            Console.WriteLine("Begin scene: " + filepath);
            environment.Refresh();
        }
    }
    
    public void Update(double dt)
    {
        if (root != null)
        {
            root.scene = this;
            root.Update(dt);
        }
    }

    public void Draw(double dt, EDrawFlags flags = 0, ICollection<ImpComp>? selected = null)
    {
        if (root != null) root.Draw(dt, flags, selected);
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

    protected override string File_GetExtension() { return "ImpScene"; }

    public int PrefabRef_Intern(A_Scene prefab)
    {
        string path = Imp.Make_Path_Local(prefab.filepath);
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
        return Imp.Asset_Load<A_Scene>(path+GetFileExtension());
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

        TTable root_tbl = tbl.get_Table("vars")?.get_Table("root");
        if (root_tbl != null)
        {
            root = ImpComp.From_Table(root_tbl, this);
            if (root != null) root.scene = this;
        }
    }

    public override TTable To_Table()
    {
        //return base.To_Table();
        prefab_refs.Clear();

        TTable vars = new();
        if(root != null) vars.Set("root",root.To_Table(this));

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