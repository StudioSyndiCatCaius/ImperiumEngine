using System.Numerics;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Enums;
using Engine.Interfaces;
using Engine.Structs;

namespace Engine.Core;

[Flags]
public enum EDrawFlags
{
    None = 0,
    Editor = 1 << 0,
    Selected = 1 << 1,
    No2D = 1 << 2,
    No3D = 1 << 3,
}

enum ECompLifeState
{
    Starting, Life, Ending,
}

public enum ECompNotify
{
    PreUpdate, PostUpdate,
}

public class ImpComp : I_Inspectable
{
    

    [ImpVar] public bool is_visible = true;
    [ImpVar] public bool cast_shadows = false;
    [ImpVar] public string name = "";
    [ImpVar] C2_Switcher substate_switcher=null;

    private bool is_visible_pref = false;

    public TScriptValue? script_instance; //script table loaded from a script file (E.G. for a "MyScene.lua" for "MyScene.ImpScene")
    public List<ImpComp> children=new();
    public ImpComp parent=null;
    public A_Scene scene=null;
    
    public byte substate=0;
    private byte substate_prev=255;
    
    private ECompLifeState life_state_engine=ECompLifeState.Starting;
    private ECompLifeState life_state_runtime=ECompLifeState.Starting;
    
    public bool is_prefab = false;
    public bool is_child_of_prefab = false;
    public bool is_builtin = false;
    public A_Scene prefab_scene;
    public TTable prefab_baseline;

    public void Update(double dt)
    {
        if(scene==null) return;

        _Notify( ECompNotify.PreUpdate, dt);
        
        // ------ ENGINE
        switch (life_state_engine)
        {
            case ECompLifeState.Starting: // IDEA: whenever a property is edited in editor, reset engine lifestate to STARTING? so OnInit is rerun
                life_state_engine = ECompLifeState.Life;
                OnInit();
                break;
            case ECompLifeState.Ending:
                OnDeinit();
                script_instance?.Call("OnDeinit");
                break;
        }
        Transform_Refresh();
        
        // ------ RUNTIME
        if (scene.is_runtime)
        {
            switch (life_state_runtime)
            {
                case ECompLifeState.Starting:
                    life_state_runtime = ECompLifeState.Life;
                    if (parent != null)
                    {
                        scene = parent.scene;
                    }
                    ImpSandbox.current?.Bind(this);
                    script_instance?.Call("OnInit");
                    OnBegin();
                    script_instance?.Call("OnBegin");
                    break;
                case ECompLifeState.Ending:
                    if (parent != null)
                    {
                        parent.Child_Remove(this);
                    }
                    is_visible = false;
                    OnEnd();
                    script_instance?.Call("OnEnd");
                    break;
                case ECompLifeState.Life:
                    if (substate != substate_prev)
                    {
                        substate_prev = substate;
                        if (substate_switcher != null) substate_switcher.current_index = substate;
                        OnSubstateChange(substate);
                        script_instance?.Call("OnSubstateChange", substate);
                    }
                    break;
            }
            OnUpdate(dt); // this is the RUNTIME update hook
            script_instance?.Call("OnUpdate", dt);
        }

        if (is_visible != is_visible_pref)
        {
            is_visible_pref = is_visible;
            OnVisibleChange(is_visible);
            script_instance?.Call("OnVisibleChange", is_visible);
        }
        
        foreach (ImpComp child in children)
        {
            child.scene = scene;
            child.Update(dt);
        }
        _Notify( ECompNotify.PostUpdate, dt);
    }
    
    public void Draw(double dt, EDrawFlags flags, ICollection<ImpComp>? selected = null)
    {
        if (!is_visible) return;
        EDrawFlags f = flags;
        if (selected != null)
        {
            ImpComp n = this;
            while (n != null)
            {
                if (selected.Contains(n))
                {
                    f |= EDrawFlags.Selected;
                    break;
                }
                n = n.parent;
            }
        }
        bool is_editor = flags.HasFlag(EDrawFlags.Editor);
        if (!flags.HasFlag(EDrawFlags.No2D)) OnDraw2D(dt, f);
        if (is_editor) OnDrawDebug(dt, false);
        if (!flags.HasFlag(EDrawFlags.No3D)) OnDraw3D(dt, f);
        if (is_editor) OnDrawDebug(dt, true);
        foreach (ImpComp child in children)
            child.Draw(dt, flags, selected);
    }
    
    public virtual void OnDrawDebug(double dt, bool drawing_3d) {}
    
    
    public void Kill()
    {
        life_state_runtime = ECompLifeState.Ending;
        life_state_engine = ECompLifeState.Ending;
    }
    
    // --------------------------------------------------
    // Child
    // --------------------------------------------------
    public void Child_Add(ImpComp child, bool builtin = false)
    {
        if (!builtin && !Allow_Children()) return;
        if (!child.Is_ChildOf(this))
        {
            children.Add(child);
            child.parent = this;
            child.is_builtin = builtin;
            if (scene != null) child.scene = scene;
        }
    }
    
    public bool Is_ChildOf(ImpComp _parent)
    {
        ImpComp _lastparent = parent;
        while (_lastparent != null)
        {
            if (_lastparent == _parent)
            {
                return true;
            }
            _lastparent = _lastparent.parent;
        }
        return false;
    }
    
    public void Child_Remove(ImpComp child)
    {
        if (child.Is_ChildOf(this))
        {
            children.Remove(child);
            child.parent = null;
            child.Kill();
        }
    }

    public void Child_RemoveAll()
    {
        List<ImpComp> copy=new (children);
        children.Clear();
        foreach (ImpComp child in copy)
        {
            child.parent = null;
            child.Kill();
        }
    }

    [ScriptCall]
    public ImpComp Child_Get(string name, bool recursive = true)
    {
        foreach (ImpComp child in children)
        {
            if (child.name == name) return child;
            if (!recursive) continue;
            ImpComp found = child.Child_Get(name, true);
            if (found != null) return found;
        }
        return null;
    }
    
    // --------------------------------------------------
    // Table Read/Write
    // --------------------------------------------------
    public static ImpComp From_Table(TTable tbl, A_Scene owner)
    {
        ImpComp comp;
        if (tbl.get_Bool("is_prefab"))
        {
            A_Scene prefab = owner.PrefabRef_Load(tbl.get_Int("prefab_id"));
            comp = prefab?.Instantiate() ?? new ImpComp();
        }
        else
        {
            Type type = TClass<object>.Resolve(tbl.get_String("type")) ?? typeof(ImpComp);
            comp = Activator.CreateInstance(type) as ImpComp ?? new();
        }

        comp.scene = owner;

        TTable vars = tbl.get_Table("vars");
        if(vars!=null) TTable.PopulateObject(vars,comp);

        foreach (object c in tbl.get_List("children"))
        {
            if(c is not TTable child_tbl) continue;
            comp.Child_Add(From_Table(child_tbl,owner));
        }

        return comp;
    }

    public TTable To_Table(A_Scene owner)
    {
        TTable tbl = new();
        if (is_prefab && prefab_scene != null)
        {
            tbl.Set("is_prefab", true);
            tbl.Set("prefab_id",owner.PrefabRef_Intern(prefab_scene));
        }
        else
        {
            tbl.Set("type", GetType().Name);
        }
        
        TTable vars = TTable.FromObject(this).get_Table("vars") ?? new();
        tbl.Set("vars", vars);

        List<object> child_list = new();
        foreach (var child in children)
        {
            if (child.is_builtin || (is_prefab && child.is_child_of_prefab)) continue;
            child_list.Add(child.To_Table(owner));
        }
        tbl.Set("children", child_list);
        
        return tbl;
    }
    
    // --------------------------------------------------
    // Prefab
    // --------------------------------------------------
    
    //clears and rebuilds this comp from a scene as prefab instance
    public void Prefab_Build(A_Scene scene)
    {
        prefab_scene = scene;
        is_prefab = true;
        Child_RemoveAll();
        if(scene?.root == null) return;
        
        TTable vars = TTable.FromObject(scene.root).get_Table("vars");
        if(vars!=null) TTable.PopulateObject(vars,this);

        foreach (var child in scene.root.children)
        {
            ImpComp child_clone = Prefab_CloneChild(child);
            child_clone.is_child_of_prefab = true;
            Child_Add(child_clone);
        }
        Prefab_CaptureBaseline();
    }

    public static bool Prefab_SameScene(A_Scene a, A_Scene b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        string pa = Prefab_NormPath(a.filepath);
        string pb = Prefab_NormPath(b.filepath);
        return pa.Length > 0 && string.Equals(pa, pb, StringComparison.OrdinalIgnoreCase);
    }

    static string Prefab_NormPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        string p = Imp.Make_Path_Local(path).Replace('\\', '/');
        const string ext = ".ImpScene";
        if (p.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            p = p[..^ext.Length];
        return p;
    }

    public void Prefab_CaptureBaseline()
    {
        prefab_baseline = TTable.FromObject(this).get_Table("vars") ?? new();
    }

    public void Prefab_Sync(ImpComp src, TTable old_tbl)
    {
        if (src == null || GetType() != src.GetType()) return;

        TTable old_vars = old_tbl?.get_Table("vars") ?? new();
        TTable baseline = prefab_baseline ?? old_vars;
        bool no_baseline = prefab_baseline == null && old_vars.data.Count == 0;
        TTable new_vars = TTable.FromObject(src).get_Table("vars") ?? new();
        TTable inst_vars = TTable.FromObject(this).get_Table("vars") ?? new();
        TTable apply = new();
        TTable next_baseline = new();
        foreach (var pair in new_vars.data)
        {
            inst_vars.data.TryGetValue(pair.Key, out object inst_val);
            baseline.data.TryGetValue(pair.Key, out object base_val);
            if (TTable.ValuesEqual(inst_val, pair.Value))
            {
                next_baseline.data[pair.Key] = pair.Value;
                continue;
            }
            bool inherit = (no_baseline && is_child_of_prefab) || TTable.ValuesEqual(inst_val, base_val);
            if (inherit)
            {
                apply.data[pair.Key] = pair.Value;
                next_baseline.data[pair.Key] = pair.Value;
            }
            else if (base_val != null)
                next_baseline.data[pair.Key] = base_val;
        }
        if (apply.data.Count > 0)
            TTable.PopulateObject(apply, this);
        prefab_baseline = next_baseline;

        List<object> old_kids = old_tbl != null ? old_tbl.get_List("children") : new();
        List<ImpComp> extras = new();
        List<ImpComp> prefab_kids = new();
        foreach (ImpComp c in children)
        {
            if (c.is_child_of_prefab) prefab_kids.Add(c);
            else extras.Add(c);
        }

        bool same = prefab_kids.Count == src.children.Count;
        if (same)
        {
            for (int i = 0; i < src.children.Count; i++)
            {
                if (!Prefab_SameLayout(prefab_kids[i], src.children[i]))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
        {
            for (int i = 0; i < src.children.Count; i++)
            {
                TTable old_child = i < old_kids.Count ? old_kids[i] as TTable : null;
                prefab_kids[i].Prefab_Sync(src.children[i], old_child);
            }
            return;
        }

        List<ImpComp> next = new();
        HashSet<ImpComp> kept = new();
        for (int i = 0; i < src.children.Count; i++)
        {
            ImpComp src_child = src.children[i];
            TTable old_child = i < old_kids.Count ? old_kids[i] as TTable : null;
            ImpComp match = i < prefab_kids.Count ? prefab_kids[i] : null;
            if (match != null && Prefab_SameLayout(match, src_child))
            {
                match.Prefab_Sync(src_child, old_child);
                next.Add(match);
                kept.Add(match);
            }
            else
            {
                ImpComp clone = Prefab_CloneChild(src_child);
                clone.is_child_of_prefab = true;
                next.Add(clone);
            }
        }

        foreach (ImpComp c in prefab_kids)
        {
            if (kept.Contains(c)) continue;
            children.Remove(c);
            c.parent = null;
            c.Kill();
        }

        children.Clear();
        foreach (ImpComp c in next)
        {
            children.Add(c);
            c.parent = this;
            c.scene = scene;
            c.is_child_of_prefab = true;
        }
        foreach (ImpComp c in extras)
        {
            children.Add(c);
            c.parent = this;
            c.scene = scene;
        }
    }

    static bool Prefab_SameLayout(ImpComp a, ImpComp b)
    {
        if (a.GetType() != b.GetType()) return false;
        if (a.is_prefab != b.is_prefab) return false;
        if (!a.is_prefab) return true;
        return Prefab_SameScene(a.prefab_scene, b.prefab_scene);
    }

    static ImpComp Prefab_CloneChild(ImpComp src)
    {
        ImpComp dst;
        if (src.is_prefab && src.prefab_scene != null)
        {
            dst = src.prefab_scene.Instantiate();
            TTable ov = TTable.FromObject(src).get_Table("vars");
            if (ov != null) TTable.PopulateObject(ov, dst);
            foreach (var src_child in src.children)
            {
                if (src_child.is_child_of_prefab) continue;
                ImpComp child_clone = Prefab_CloneChild(src_child);
                child_clone.is_child_of_prefab = true;
                dst.Child_Add(child_clone);
            }
            dst.Prefab_CaptureBaseline();
        }
        else
        {
            dst = Activator.CreateInstance(src.GetType()) as ImpComp ?? new();
            TTable cv = TTable.FromObject(src).get_Table("vars");
            if (cv != null) TTable.PopulateObject(cv, dst);
            foreach (var child in src.children)
            {
                ImpComp child_clone = Prefab_CloneChild(child);
                child_clone.is_child_of_prefab = true;
                dst.Child_Add(child_clone);
            }
            dst.Prefab_CaptureBaseline();
        }
        return dst;
    }
    
    
    // --------------------------------------------------
    // VIRTUALS
    // --------------------------------------------------
    
    public virtual void _Notify(ECompNotify notify, double dt) { }
    
    //Cursor target is whatever the mouse/cursor is currently over
    public virtual void _NotifyAs_CursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt) { }
    public virtual void _InputAs_CursorTarget(ImpPlayer player, EInputKey key, EInputState state, double dt) { }
    //Focus target is whatever the mouse/cursor hast currently selected
    public virtual void _NotifyAs_FocusTarget(ImpPlayer player, ENotifyGeneric notify, double dt) { }
    public virtual void _InputAs_FocusTarget(ImpPlayer player, EInputKey key, EInputState state, double dt) { }

    public virtual void _NotifyAs_DragTarget(ImpPlayer player, ImpComp drop_target, ENotifyGeneric notify, double dt) { }
    public virtual void _InputAs_DragTarget(ImpPlayer player, EInputKey key, EInputState state, double dt) { }
    
    public virtual bool Dragging_IsAllowed() { return false; } //can a player drag this?
    public virtual Imp2D Dragging_GetPreview() { return null; } //the on screen preview/representative of the dragged object that will follow the cursor
    
    // Rebuilds global_transform from the parent's. Runs before OnUpdate, and the update walks
    // parents before children, so a child always composes against an up-to-date parent global.
    protected virtual void Transform_Refresh() { }
    
    public virtual void OnDraw2D(double dt, EDrawFlags flags = 0) { }
    public virtual void OnDraw3D(double dt, EDrawFlags flags = 0) { }
    
    public virtual bool Is_Singleton() { return false; } // only one instance of this component
    public virtual bool Allow_Children() { return true; }

    public void Inspectable_OnPropertyEdit(string name, object oldValue, object newValue)
    {
        OnInit();
    }

    // --- Script Overrides
    
    [ScriptHook] public virtual void OnInit() {} //equal of onConstruct/ConstructionScript on UE
    [ScriptHook] public virtual void OnDeinit() {} //equal of onDestruct on UE
    
    [ScriptHook] public virtual void OnBegin() { }
    [ScriptHook] public virtual void OnEnd() {}
    [ScriptHook] public virtual void OnUpdate(double dt) { }
    
    [ScriptHook] public virtual void OnVisibleChange(bool _visible) { }
    [ScriptHook] public virtual void OnSubstateChange(byte _substate) { }
    
    [ScriptHook] public virtual void Input_Pressed(ImpPlayer player,TLabel ia,Vector3 axis) { }
    [ScriptHook] public virtual void Input_Released(ImpPlayer player,TLabel ia, Vector3 axis) { }
    [ScriptHook] public virtual void Input_Down(ImpPlayer player,TLabel ia,Vector3 axis, double dt) { }
}

public class Imp1D : ImpComp { }