using System.Diagnostics;
using System.Numerics;
using Engine.Assets;
using Engine.Comps._2D;
using Engine.Enums;
using Engine.Globals;
using Engine.Interfaces;
using Engine.Structs;
using Engine;

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

enum ECompLifeState : byte
{
    Starting, Life, Ending,
}

public enum ECompNotify : byte
{
    PreUpdate, PostUpdate,
}

public class ImpComp : I_Inspectable, I_Input, I_File 
{
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATIC
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    static readonly Dictionary<Type, Type[]> _type_chain = new();
    static readonly ImpComp[] _none = Array.Empty<ImpComp>();
    public static Action<ImpComp, double>? profile_update;
    public static Action<ImpComp, double>? profile_draw;

    // C3_Mesh, Imp3D, ImpComp — cached so register/unregister is a tight loop.
    internal static Type[] TypeChain(Type t)
    {
        if (t == null) return Array.Empty<Type>();
        if (_type_chain.TryGetValue(t, out Type[] chain)) return chain;
        List<Type> list = new(4);
        for (Type cur = t; cur != null && typeof(ImpComp).IsAssignableFrom(cur); cur = cur.BaseType)
            list.Add(cur);
        chain = list.ToArray();
        _type_chain[t] = chain;
        return chain;
    }

    // when scene is null, uses App.scene_current
    [ScriptCall][Title("Comp - Get First Of Class")]
    public static ImpComp GetFirstOfClass(TClass<ImpComp> Class, A_Scene scene = null)
    {
        HashSet<ImpComp> set = IndexOf(Class.Get(), scene);
        if (set == null) return null;
        foreach (ImpComp c in set)
            return c;
        return null;
    }

    [ScriptCall][Title("Comp - Get All Of Class")]
    public static ImpComp[] GetAllOfClass(TClass<ImpComp> Class, A_Scene scene = null)
    {
        HashSet<ImpComp> set = IndexOf(Class.Get(), scene);
        if (set == null || set.Count == 0) return _none;
        ImpComp[] arr = new ImpComp[set.Count];
        set.CopyTo(arr);
        return arr;
    }

    static HashSet<ImpComp> IndexOf(Type t, A_Scene scene)
    {
        if (t == null) return null;
        scene ??= App.scene_current;
        return scene?.CompIndex_Of(t);
    }
    
    
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    
    // ===========================================================================================
    // Imp Vars
    // ===========================================================================================
    [ImpVar] public string name = "";
    [ImpVar] C2_Switcher substate_switcher=null;
    [ImpVar] public bool is_visible = true;
    [ImpVar] public bool auto_globalize = false;
    [ImpVar(Hidden = true)] public bool editor_locked = false;
    [ImpVar(Hidden = true)] public TGuid64 id=new ();

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
    A_Scene _scene;
    public A_Scene scene
    {
        get => _scene;
        set
        {
            if (_scene == value) return;
            _scene?.CompIndex_Unreg(this);
            _scene = value;
            _scene?.CompIndex_Reg(this);
        }
    }
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
    public bool is_runtime = false;
    public A_Scene prefab_scene;
    public TTable prefab_baseline;

    // ===========================================================================================
    // INIT / DISPOSE
    // ===========================================================================================
    public ImpComp(IEnumerable<ImpComp> _children = null)
    {
        id = TGuid64.New();
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
            {
                if(scene==null) return;

                bool prof_u = profile_update != null;
                long t_u = prof_u ? Stopwatch.GetTimestamp() : 0;

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
                            if (script_instance == null)
                                ImpSandbox.current?.Bind(this);
                            OnBegin();
                            on_begin?.Invoke(this);
                            script_instance?.Call("OnBegin");
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
                            script_instance?.Call("OnEnd");
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
                    script_instance?.Call("OnUpdate");
                    script_instance?.Tick(dt);
                }

                if (is_visible != is_visible_pref)
                {
                    is_visible_pref = is_visible;
                    OnVisibleChange(is_visible);
                }
                
                _Notify( ECompNotify.PostUpdate, dt);

                if (prof_u)
                    profile_update?.Invoke(this, (Stopwatch.GetTimestamp() - t_u) * 1000.0 / Stopwatch.Frequency);
                
                break;
            }
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

    public void AssignScene(A_Scene s, bool force = false)
    {
        if (!force && scene == s) return;
        scene = s;
        for (int i = 0; i < children.Count; i++)
            children[i].AssignScene(s, force);
    }

    void AfterChildrenChanged()
    {
        for (int i = 0; i < children.Count; i++)
        {
            ImpComp c = children[i];
            c.parent = this;
            c.sibling_index = i;
            if (c.scene != scene) c.AssignScene(scene, true);
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
        bool prof_d = profile_draw != null;
        long t_d = prof_d ? Stopwatch.GetTimestamp() : 0;
        
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

        if (prof_d)
            profile_draw?.Invoke(this, (Stopwatch.GetTimestamp() - t_d) * 1000.0 / Stopwatch.Frequency);
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
            child.AssignScene(scene, true);
            if (scene != null && scene.is_running) child.is_runtime = true;
            child.sibling_index = children.Count - 1;
            if (child.id.IsNone) child.id = TGuid64.New();
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
        if (child.scene != scene) child.AssignScene(scene, true);
        if (scene != null && scene.is_running) child.is_runtime = true;
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
            child.AssignScene(null, true);
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
            copy[i].AssignScene(null, true);
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

    public ImpComp Comp_Find(TGuid64 find_id)
    {
        if (find_id.IsNone) return null;
        if (id.Equals(find_id)) return this;
        for (int i = 0; i < children.Count; i++)
        {
            ImpComp found = children[i].Comp_Find(find_id);
            if (found != null) return found;
        }
        return null;
    }

    public void Id_RenewTree()
    {
        id = TGuid64.New();
        for (int i = 0; i < children.Count; i++)
            children[i].Id_RenewTree();
    }

    public static ImpComp Find(TGuid64 find_id, string scene_path = "")
    {
        if (find_id.IsNone) return null;

        ImpComp Search(A_Scene s)
        {
            if (s?.root == null) return null;
            if (!string.IsNullOrEmpty(scene_path))
            {
                if (string.IsNullOrEmpty(s.filepath)) return null;
                string a = GFile.Make_Path_Local(s.filepath).Replace('\\', '/');
                string b = GFile.Make_Path_Local(scene_path).Replace('\\', '/');
                if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return null;
            }
            return s.root.Comp_Find(find_id);
        }

        ImpComp hit = Search(App.scene_current);
        if (hit != null) return hit;
        hit = Search(App.scene_next);
        if (hit != null) return hit;
        if (App.scenes_global != null)
        {
            for (int i = 0; i < App.scenes_global.Count; i++)
            {
                hit = Search(App.scenes_global[i]);
                if (hit != null) return hit;
            }
        }
        foreach (var pair in App.assets)
        {
            if (pair.Value is not A_Scene sc) continue;
            hit = Search(sc);
            if (hit != null) return hit;
        }
        if (string.IsNullOrEmpty(scene_path)) return null;
        return Search(GAsset.Asset_Load<A_Scene>(scene_path));
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
        if (comp.id.IsNone) comp.id = TGuid64.New();
        else TGuid64.Seen(comp.id);

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
            if (child.is_builtin || child.is_runtime || (is_prefab && child.is_child_of_prefab)) continue;
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
        id = TGuid64.New();

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
            c.AssignScene(null, true);
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
            dst.id = TGuid64.New();
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
            dst.id = TGuid64.New();
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