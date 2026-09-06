using System.Numerics;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Enums;
using Engine.Globals;
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

[Flags]
public enum ECompProcess : byte
{
    None = 0,
    Update = 1 << 0,
    Draw3D = 1 << 1,
    Draw2D = 1 << 2,
    Cursor = 1 << 3,
}

enum ECompLifeState
{
    Starting, Life, Ending,
}

public enum ECompNotify
{
    PreUpdate, PostUpdate,
}

public class ImpComp : I_Inspectable, I_Input, I_File
{
    // ===========================================================================================
    // Imp Vars
    // ===========================================================================================
    [ImpVar] public string name = "";
    [ImpVar] C2_Switcher substate_switcher=null;
    [ImpVar] public bool is_visible = true;
    [ImpVar] public bool auto_globalize = false;
    [ImpVar(Hidden = true)] public bool editor_locked = false;

    // ===========================================================================================
    // ACTIONS
    // ===========================================================================================

    public Action<ImpComp> on_begin;
    public Action<ImpComp> on_end;
    
    // ===========================================================================================
    // VARS
    // ===========================================================================================
    private bool is_visible_pref = false;
    
    public TScriptValue? script_instance; // bound later by C# / Vis sandbox
    public List<ImpComp> children=new();
    public ImpComp parent=null;
    public A_Scene scene=null;
    public int sibling_index;
    internal ECompProcess _subtree_kinds;
    internal bool _kinds_inited;
    
    public byte substate=0;
    private byte substate_prev=255;
    
    private ECompLifeState life_state_engine=ECompLifeState.Starting;
    private ECompLifeState life_state_runtime=ECompLifeState.Starting;
    
    public bool is_prefab = false;
    public bool is_child_of_prefab = false;
    public bool is_builtin = false;
    public A_Scene prefab_scene;
    public TTable prefab_baseline;

    // ===========================================================================================
    // INIT
    // ===========================================================================================
    public ImpComp(IEnumerable<ImpComp> _children = null)
    {
        if (_children != null)
        {
            foreach (ImpComp child in _children)
            {
                Child_Add(child);
            }
        }
    }
    
    // ===========================================================================================
    // UPDATE
    // ===========================================================================================
    public virtual void ProcessNotify(ENotifyProcess notify, double dt)
    {
        switch (notify)
        {
            // ---------------------------------------------------------------------------------------------
            // UPDATE
            // ---------------------------------------------------------------------------------------------
            case ENotifyProcess.Update:
                
                if(scene==null) return;

                _Notify( ECompNotify.PreUpdate, dt);
                
                // ---------------------------------------------------------
                // ENGINE
                // ---------------------------------------------------------
                switch (life_state_engine)
                {
                    case ECompLifeState.Starting: // IDEA: whenever a property is edited in editor, reset engine lifestate to STARTING? so OnInit is rerun
                        life_state_engine = ECompLifeState.Life;
                        OnInit();
                        break;
                    case ECompLifeState.Ending:
                        OnDeinit();
                        break;
                }
                Transform_Refresh();
                
                // ---------------------------------------------------------
                // RUNTIME
                // ---------------------------------------------------------
                if (scene.is_running)
                {
                    switch (life_state_runtime)
                    {
                        // ---------------------------------------------------------
                        // BEGIN
                        // ---------------------------------------------------------
                        case ECompLifeState.Starting:
                            life_state_runtime = ECompLifeState.Life;
                            if (parent != null)
                            {
                                scene = parent.scene;
                            }

                            if (auto_globalize)
                            {
                                App.globalized_comps.Add(name,this);
                            }
                            OnBegin();
                            on_begin?.Invoke(this);
                            break;
                        // ---------------------------------------------------------
                        // END
                        // ---------------------------------------------------------
                        case ECompLifeState.Ending:
                            if (parent != null)
                            {
                                parent.Child_Remove(this);
                            }
                            is_visible = false;
                            OnEnd();
                            on_end?.Invoke(this);
                            break;
                        // ---------------------------------------------------------
                        // Update/Tick
                        // ---------------------------------------------------------
                        case ECompLifeState.Life:
                            if (substate != substate_prev)
                            {
                                substate_prev = substate;
                                if (substate_switcher != null) substate_switcher.current_index = substate;
                                OnSubstateChange(substate);
                            }
                            break;
                    }
                    OnUpdate(dt);
                }

                if (is_visible != is_visible_pref)
                {
                    is_visible_pref = is_visible;
                    OnVisibleChange(is_visible);
                }
                
                _Notify( ECompNotify.PostUpdate, dt);
                
                break;
            // ---------------------------------------------------------------------------------------------
            // DRAW 3D
            // ---------------------------------------------------------------------------------------------
            case ENotifyProcess.Draw3D: Draw(dt, 0, EDrawFlags.None); break;
            // ---------------------------------------------------------------------------------------------
            // DRAW 2D
            // ---------------------------------------------------------------------------------------------
            case ENotifyProcess.Draw2D: Draw(dt, 1, EDrawFlags.None); break;
        }
        
        ECompProcess need = NotifyMask(notify);
        for (int i = 0; i < children.Count; i++)
        {
            ImpComp child = children[i];
            if (child._kinds_inited && (child._subtree_kinds & need) == 0) continue;
            child.ProcessNotify(notify, dt);
        }
    }

    static ECompProcess NotifyMask(ENotifyProcess notify) => notify switch
    {
        ENotifyProcess.Update => ECompProcess.Update,
        ENotifyProcess.Draw3D => ECompProcess.Draw3D,
        ENotifyProcess.Draw2D => ECompProcess.Draw2D,
        ENotifyProcess.CursorStack => ECompProcess.Cursor,
        _ => ECompProcess.None,
    };

    protected virtual ECompProcess ProcessKinds => ECompProcess.Update;

    public void AssignScene(A_Scene s)
    {
        if (scene == s) return;
        scene = s;
        for (int i = 0; i < children.Count; i++)
            children[i].AssignScene(s);
    }

    void AfterChildrenChanged()
    {
        for (int i = 0; i < children.Count; i++)
        {
            ImpComp c = children[i];
            c.parent = this;
            c.scene = scene;
            c.sibling_index = i;
            if (!c._kinds_inited)
            {
                c._subtree_kinds = c.ProcessKinds;
                c._kinds_inited = true;
            }
        }
        RefreshSubtreeKinds();
    }

    internal void RefreshSubtreeKinds()
    {
        ECompProcess k = ProcessKinds;
        for (int i = 0; i < children.Count; i++)
            k |= children[i]._subtree_kinds;
        if (_kinds_inited && k == _subtree_kinds) return;
        _subtree_kinds = k;
        _kinds_inited = true;
        parent?.RefreshSubtreeKinds();
    }
    
    

    public void Draw(double dt, byte pass, EDrawFlags flags)
    {
        if (!is_visible) return;
        bool is_editor = (flags & EDrawFlags.Editor) != 0;
        
        switch (pass)
        {
            case 0: //3D
                if ((flags & EDrawFlags.No3D) == 0) OnDraw3D(dt, flags);
                if (is_editor) OnDrawDebug(dt, true);        
                break;
            case 1: //2D
                if ((flags & EDrawFlags.No2D) == 0) OnDraw2D(dt, flags);
                if (is_editor) OnDrawDebug(dt, false);
                break;
        }
    }
    
    public virtual void OnDrawDebug(double dt, bool drawing_3d) {}
    
    
    public void Kill()
    {
        life_state_runtime = ECompLifeState.Ending;
        life_state_engine = ECompLifeState.Ending;
    }
    
    // ========================================================================================================
    // Children
    // ========================================================================================================
    public void Child_Add(ImpComp child, bool builtin = false)
    {
        if (!builtin && !Allow_Children()) return;
        if (!child.Is_ChildOf(this))
        {
            children.Add(child);
            child.parent = this;
            child.is_builtin = builtin;
            child.scene = scene;
            child.sibling_index = children.Count - 1;
            if (!child._kinds_inited)
            {
                child._subtree_kinds = child.ProcessKinds;
                child._kinds_inited = true;
            }
            RefreshSubtreeKinds();
        }
    }

    public void Child_Insert(ImpComp child, int index)
    {
        if (child == null || child == this || Is_ChildOf(child)) return;
        ImpComp old = child.parent;
        bool same = old == this;
        if (!same && !Allow_Children()) return;
        old?.children.Remove(child);
        if (index < 0) index = 0;
        if (index > children.Count) index = children.Count;
        children.Insert(index, child);
        if (old != null && !same) old.AfterChildrenChanged();
        AfterChildrenChanged();
        if (child.scene != scene) child.AssignScene(scene);
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
            AfterChildrenChanged();
        }
    }

    public void Child_RemoveAll()
    {
        ImpComp[] copy = children.Count == 0 ? Array.Empty<ImpComp>() : children.ToArray();
        children.Clear();
        _subtree_kinds = ProcessKinds;
        _kinds_inited = true;
        parent?.RefreshSubtreeKinds();
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i].parent = null;
            copy[i].Kill();
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
    
    // ========================================================================================================
    // Table Read/Write
    // ========================================================================================================
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
    
    // ========================================================================================================
    // Prefab
    // ========================================================================================================
    
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
        string p = GFile.Make_Path_Local(path).Replace('\\', '/');
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
            c.is_child_of_prefab = true;
        }
        foreach (ImpComp c in extras)
            children.Add(c);
        AfterChildrenChanged();
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
    
    // ==========================================================================================
    // Drag & Drop
    // ==========================================================================================
    public virtual bool Dragging_IsAllowed() { return false; } //can a player drag this?
    public virtual Imp2D Dragging_GetPreview() { return null; } //the on screen preview/representative of the dragged object that will follow the cursor
    
    public virtual void Dragging_OnDrop(ImpPlayer player, ImpComp drop_target, Vector2 cursor_pos, double dt) { }

    public void _Input_Notif_Key(ImpPlayer player, EInputKey key, EInputState state, double dt) { }
    public void _Input_Notif_Action(ImpPlayer player, TLabel action, EInputState state, Vector3 axis, double dt)
    {
        switch (state)
        {
            case EInputState.Down:
                Input_Down(player,action,axis,dt);
                break;
            case EInputState.Pressed:
                Input_Pressed(player,action,axis);
                break;
            case EInputState.Released:
                Input_Released(player,action,axis);
                break;
        }
    }

    // Rebuilds derived transform/bounds from the parent. Runs before OnUpdate; parents first.
    protected virtual void Transform_Refresh() { }
    
    public virtual void OnDraw2D(double dt, EDrawFlags flags = 0) { }
    public virtual void OnDraw3D(double dt, EDrawFlags flags = 0) { }
    
    public virtual bool Is_Singleton() { return false; } // only one instance of this component
    public virtual bool Allow_Children() { return true; }

    public bool Editor_IsLocked()
    {
        ImpComp n = this;
        while (n != null)
        {
            if (n.editor_locked) return true;
            n = n.parent;
        }
        return false;
    }

    // ------------------------------------------------------------------------------------
    // Inspector / Property
    // ------------------------------------------------------------------------------------
    public void Inspectable_OnPropertyEdit(string name, object oldValue, object newValue)
    {
        OnInit();
    }

    public Type[] File_GetFavoriteSubTypes()
    {
        Type[] output=new []
        {
            typeof(ImpComp),
            typeof(Imp2D),
            typeof(Imp3D),
        };

        return output;
    }

    // ------------------------------------------------------------------------------------
    // --- Script Overrides
    // ------------------------------------------------------------------------------------
    
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