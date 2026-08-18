using System.Numerics;
using System.Reflection;
using ImperiumEngine.Assets;
using ImperiumEngine.Comps;
using ImperiumEngine.Comps._1D;
using ImperiumEngine.Comps._2D;
using ImperiumEngine.Enums;
using ImperiumEngine.Script;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine;

[Flags]
public enum EDrawFlags
{
    Editor, // what to draw when in editor view (Toggle in editor with "G")
    Selected, //what to draw when selected in editor
}

public class ImpComp
{
    // #################################################################################
    // Static
    // #################################################################################

    // Class names already written into saved scenes. A rename here silently downgrades every
    // node that used the old name to a bare ImpComp on load - the comp keeps its name and
    // children but loses transform and everything else the real class declared.
    static readonly Dictionary<string, string> _legacy_names = new()
    {
        ["ImpComp2D"] = nameof(Imp2D),
        ["ImpComp3D"] = nameof(Imp3D),
    };

    // Comp currently inside Update/OnUpdate. Key_Is* uses this so a dialog (or any
    // input_hog) can swallow keys for everyone else without each hotkey checking.
    public static ImpComp? Updating { get; private set; }

    public static Type? Type_FromName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (_legacy_names.TryGetValue(name, out string? renamed)) name = renamed;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type?[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            foreach (Type? t in types)
            {
                if (t == null || t.Name != name || t.IsAbstract) continue;
                if (!typeof(ImpComp).IsAssignableFrom(t)) continue;
                return t;
            }
        }
        return null;
    }
    
    
    // #################################################################################
    // Class
    // #################################################################################
    
    [Category("Component")][ImpVar(Edit = EImpVarEdit.ReadOnly)]
    public string name; //should probably be a TLabel later?

    [Category("Component")][ImpVar] public bool is_visible=true;
    [Category("Component")][ImpVar] public bool is_locked=false;
    [Category("Component")][ImpVar] public bool children_editable=true;
    [Category("Component")][ImpVar] public A_PopupConfig popup_config=null;
    [Category("Component")][ImpVar] TClass<Imp2D> tooltip_class;
    [Category("Imp")][ImpVar] public A_Script script_override=null;

    [ImpVar(Hidden = true)] public A_Script script_builtin=new A_Script();

    public A_Script Script_Get()
    {
        if (script_override != null)
        {
            return script_override;
        }
        if (script_builtin == null)
        {
            script_builtin = new A_Script();
        }
        if (string.IsNullOrEmpty(script_builtin.parent_type.class_name))
        {
            script_builtin.parent_type = new TClass<Object>(GetType());
        }
        return script_builtin;
    }
    
    private bool is_destroying=false;
    
    public readonly List<ImpComp> children=new();
    public ImpComp parent=null;
    ImpScene _scene;
    public ImpScene scene
    {
        get => _scene;
        set
        {
            if (_scene == value) return;
            _scene = value;
            for (int i = 0; i < children.Count; i++)
                children[i].scene = value;
        }
    }

    ImpGame _game_owner;
    public ImpGame game_owner
    {
        get => _game_owner;
        set
        {
            if (_game_owner == value)
            {
                return;
            }
            _game_owner = value;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].game_owner = value;
            }
        }
    }
    
    public ImpPlayer input_owner=null;

    // Pulse script running on this comp. A scene script is bound to its root comp (ImpScene.RBegin
    // sets this), so events that arrive at the comp — input — reach the scene graph.
    public ImpScriptVM impScriptVm;

    public void Script_Event(string member, object[] args)
    {
        if (impScriptVm == null)
        {
            return;
        }
        impScriptVm.Event_Run(member, args);
    }

    public TRef<ImpScene> packed;
    public ImpComp packed_from;

    public bool IsInstanceRoot => packed_from == this && (packed.Get() != null || !string.IsNullOrEmpty(packed.path));
    public bool IsPackedForeign => packed_from != null && packed_from != this;

    static readonly Dictionary<Type, FieldInfo[]> _owned_fields = new();

    public static FieldInfo[] OwnedFields(Type type)
    {
        if (type == null)
        {
            return Array.Empty<FieldInfo>();
        }
        if (_owned_fields.TryGetValue(type, out FieldInfo[] hit))
        {
            return hit;
        }
        List<FieldInfo> list = new();
        FieldInfo[] all = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < all.Length; i++)
        {
            FieldInfo f = all[i];
            if (f.IsInitOnly || f.IsLiteral)
            {
                continue;
            }
            if (f.Name == "parent" || f.Name == "packed_from")
            {
                continue;
            }
            if (!typeof(ImpComp).IsAssignableFrom(f.FieldType))
            {
                continue;
            }
            list.Add(f);
        }
        FieldInfo[] arr = list.ToArray();
        _owned_fields[type] = arr;
        return arr;
    }

    public FieldInfo OwnedFieldOf(ImpComp child)
    {
        if (child == null)
        {
            return null;
        }
        FieldInfo[] fields = OwnedFields(GetType());
        for (int i = 0; i < fields.Length; i++)
        {
            if (ReferenceEquals(fields[i].GetValue(this), child))
            {
                return fields[i];
            }
        }
        return null;
    }

    public bool IsOwned
    {
        get
        {
            if (parent == null)
            {
                return false;
            }
            return parent.OwnedFieldOf(this) != null;
        }
    }

    public static ImpComp OutlinerHost(ImpComp c)
    {
        ImpComp n = c;
        while (n != null && (n.IsOwned || n.IsPackedForeign))
        {
            n = n.parent;
        }
        return n;
    }

    public void Owned_Bind()
    {
        FieldInfo[] fields = OwnedFields(GetType());
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].GetValue(this) is not ImpComp c)
            {
                continue;
            }
            if (c == this || c.parent != null)
            {
                continue;
            }
            Child_Add(c);
        }
    }

    // Bare ImpComp / Imp2D / Imp3D with kids is a group pivot, not a clickable volume.
    public bool IsGroupPivot
    {
        get
        {
            Type t = GetType();
            if (t != typeof(ImpComp) && t != typeof(Imp2D) && t != typeof(Imp3D)) return false;
            return children.Count > 0;
        }
    }

    public ImpComp()
    {
        name = GetType().Name;
        if (script_builtin == null)
        {
            script_builtin = new A_Script();
        }
        script_builtin.parent_type = new TClass<Object>(GetType());
    }

    /// <summary>
    /// A name not already used by one of <paramref name="parent"/>'s children. If `desired` is
    /// free it comes back unchanged. Otherwise its trailing digits are stripped to get a base
    /// and the lowest free number is appended: mesh -> mesh1 -> mesh2. Numbers freed by
    /// deletion get reused, so the sequence stays tight instead of climbing forever.
    /// </summary>
    public static string Name_Unique(ImpComp parent, string desired)
    {
        if (parent == null) return desired;
        if (string.IsNullOrEmpty(desired)) desired = nameof(ImpComp);
        if (!Name_Taken(parent, desired)) return desired;

        int cut = desired.Length;
        while (cut > 0 && char.IsAsciiDigit(desired[cut - 1])) cut--;
        // An all-digit name has no base left to number, so keep it whole: "12" -> "121".
        string base_name = cut > 0 ? desired[..cut] : desired;

        // Bounded by the child count - one of the first Count+1 numbers must be free.
        for (int n = 1; ; n++)
        {
            string candidate = base_name + n;
            if (!Name_Taken(parent, candidate)) return candidate;
        }
    }

    static bool Name_Taken(ImpComp parent, string name)
    {
        for (int i = 0; i < parent.children.Count; i++)
            if (string.Equals(parent.children[i].name, name, StringComparison.Ordinal)) return true;
        return false;
    }


    public void Child_Add(ImpComp child)
    {
        if (child == null || child == this) return;
        if (child.IsPackedForeign) return;
        if (IsPackedForeign) return;
        if (IsInstanceRoot && OwnedFieldOf(child) == null && !children.Contains(child)) return;
        if (children.Contains(child))
        {
            child.parent = this;
            child.scene = scene;
            child.game_owner = game_owner;
            Imp2D.Layout_Invalidate();
            return;
        }
        child.Detach();
        children.Add(child);
        child.parent = this;
        child.scene = scene;
        child.game_owner = game_owner;
        Imp2D.Layout_Invalidate();
    }

    public void Child_Insert(int index, ImpComp child)
    {
        if (child == null || child == this) return;
        if (child.IsPackedForeign) return;
        if (IsPackedForeign || IsInstanceRoot) return;
        if (children.Contains(child))
        {
            int cur = children.IndexOf(child);
            children.RemoveAt(cur);
            if (index > cur) index--;
        }
        else child.Detach();
        if (index < 0) index = 0;
        if (index > children.Count) index = children.Count;
        children.Insert(index, child);
        child.parent = this;
        child.scene = scene;
        child.game_owner = game_owner;
        Imp2D.Layout_Invalidate();
    }

    public void Detach()
    {
        if (parent == null) return;
        if (IsOwned) return;
        parent.children.Remove(this);
        parent = null;
        scene = null;
        game_owner = null;
        Imp2D.Layout_Invalidate();
    }

    public void Reparent(ImpComp new_parent, int index = -1)
    {
        if (new_parent == null || new_parent == this || IsAncestorOf(new_parent)) return;
        if (IsPackedForeign || IsOwned) return;
        if (new_parent.IsPackedForeign || new_parent.IsInstanceRoot || new_parent.IsOwned) return;

        TTransform3? world3 = this is Imp3D c3 ? c3.Transform_Get(true) : null;
        TTransform2? world2 = this is Imp2D c2 ? c2.Transform_Get(true) : null;

        if (index < 0) new_parent.Child_Add(this);
        else new_parent.Child_Insert(index, this);

        if (world3.HasValue && this is Imp3D a3) a3.Transform_Set(world3.Value, true);
        if (world2.HasValue && this is Imp2D a2) a2.Transform_Set(world2.Value, true);
    }

    public bool IsAncestorOf(ImpComp other)
    {
        for (ImpComp p = other; p != null; p = p.parent)
            if (p == this) return true;
        return false;
    }

    public bool IsDescendantOf(ImpComp other)
    {
        return other != null && other.IsAncestorOf(this);
    }

    /// <summary>
    /// True when this comp and every ancestor is visible. Hiding a comp does not touch the
    /// is_visible of its children, so anything asking "am I on screen" has to walk up.
    /// </summary>
    [ScriptCall] public bool IsVisibleInTree()
    {
        for (ImpComp n = this; n != null; n = n.parent)
            if (!n.is_visible) return false;
        return true;
    }

    public void Child_RemoveAt(int index)
    {
        Child_GetAt(index).Destroy();
    }
    public ImpComp Child_GetAt(int index)
    {
        return children[index];
    }
    public void Child_RemoveAll()
    {
        while (children.Count > 0)
            children[0].Destroy();
    }

    [ScriptCall] public void Destroy()
    {
        if (is_destroying) return;
        is_destroying = true;
        OnDestroy();
        while (children.Count > 0)
            children[0].Destroy();
        if (parent != null)
        {
            parent.children.Remove(this);
            parent = null;
        }
        scene = null;
        game_owner = null;
        is_visible = false;
        Imp2D.Layout_Invalidate();
    }

    static readonly Dictionary<Type, FieldInfo[]> _clone_fields = new();

    static FieldInfo[] CloneFields(Type type)
    {
        if (_clone_fields.TryGetValue(type, out FieldInfo[] hit)) return hit;
        List<FieldInfo> list = new();
        foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (f.IsInitOnly || f.IsLiteral) continue;
            if (f.Name is "parent" or "children" or "input_owner" or "is_destroying" or "_scene" or "_game_owner" or "packed_from") continue;
            // Layout cache (Imp2D). Copying a stamp would let the clone answer with the
            // original's rect until the next epoch bump, so leave it at 0 and recompute.
            if (f.Name.StartsWith("_e_") || f.Name.StartsWith("_c_")) continue;
            list.Add(f);
        }
        FieldInfo[] arr = list.ToArray();
        _clone_fields[type] = arr;
        return arr;
    }

    public ImpComp Clone(ImpScene skip_self = null)
    {
        if (Activator.CreateInstance(GetType()) is not ImpComp copy) return null;
        Owned_Bind();
        copy.Owned_Bind();
        Clone_Into(copy, skip_self);
        if (skip_self == null && (copy.packed.Get() != null || !string.IsNullOrEmpty(copy.packed.path)))
            ImpScene.BindPackedTree(copy, copy);
        return copy;
    }

    void Clone_Into(ImpComp copy, ImpScene skip_self)
    {
        if (copy == null)
        {
            return;
        }
        FieldInfo[] owned = OwnedFields(GetType());
        foreach (FieldInfo f in CloneFields(GetType()))
        {
            bool is_owned_slot = false;
            for (int i = 0; i < owned.Length; i++)
            {
                if (owned[i] == f)
                {
                    is_owned_slot = true;
                    break;
                }
            }
            if (is_owned_slot)
            {
                continue;
            }
            object val = f.GetValue(this);
            if (f.Name == "option_button" && val == this)
            {
                f.SetValue(copy, copy);
                continue;
            }
            if (val is A_Script sc && string.IsNullOrEmpty(sc.filepath))
            {
                f.SetValue(copy, sc.Clone());
                continue;
            }
            f.SetValue(copy, val);
        }
        copy.input_owner = null;
        copy.packed_from = null;
        for (int i = 0; i < children.Count; i++)
        {
            ImpComp kid = children[i];
            if (skip_self != null && ImpScene.IsSelfInstance(kid, skip_self))
            {
                continue;
            }
            FieldInfo slot = OwnedFieldOf(kid);
            if (slot != null)
            {
                ImpComp dst = slot.GetValue(copy) as ImpComp;
                if (dst == null)
                {
                    dst = kid.Clone(skip_self);
                    if (dst == null)
                    {
                        continue;
                    }
                    slot.SetValue(copy, dst);
                    copy.Child_Add(dst);
                }
                else
                {
                    kid.Clone_Into(dst, skip_self);
                }
                continue;
            }
            ImpComp ck = kid.Clone(skip_self);
            if (ck != null)
            {
                copy.Child_Add(ck);
            }
        }
    }
    
    
    public void Update(double dt, bool is_runtime)
    {
        ImpComp prev = Updating;
        Updating = this;
        try
        {
            // -------- INPUT
            if (input_owner != null)
            {
                if (input_owner.input_hog == null)
                {
                    // A player drives one session at a time: PIE comps stay dead until the game
                    // view is clicked into, and editor-owned comps stop the moment it is.
                    // A null owner counts as host — Destroy() nulls game_owner, so treating it
                    // as "always allowed" would keep feeding a torn-down tree.
                    ImpGame owner = game_owner;
                    if (owner == null)
                    {
                        owner = ImpGame.Get(ImpGame.ID_HOST);
                    }
                    if (owner == input_owner.TargetGame_Get())
                    {
                        Update_Input(dt,input_owner);
                    }
                }
            }

            // -------- PROCESS
            // Layout cache lives for the whole Update phase (ImpApp bumps the epoch once).
            // Direct field writes (C2_List size/position) are visible to later children
            // because Dimensions_Get reads those fields; a per-comp epoch wipe was
            // destroying the cache (~2 bumps x every comp) and made Fill rects jitter.
            if (is_runtime) OnUpdate(dt);
            if (ImpProfiler.enabled) ImpProfiler.comp_count++;

            int n = children.Count;
            ImpComp[] snap = ChildSnap_Rent(n);
            for (int i = 0; i < n; i++)
            {
                ImpComp c = snap[i];
                if (c.parent != this) continue;
                c.Update(dt, is_runtime);
            }
            ChildSnap_Return(snap, n);
        }
        finally { Updating = prev; }
    }
    
    public void Update_Input(double dt, ImpPlayer _player)
    {
        foreach (var keystate in _player.action_states)
        {
            TLabel action = keystate.Key;
            EInputState state = keystate.Value;
            if (state == EInputState.None) continue;
            Vector3 axis = _player.InputAction_GetAxis(action);
            // Arg order must match the [PulseOverride] signature — the event node hands its
            // output pins out in parameter order.
            switch (state)
            {
                case EInputState.Pressed:
                    Input_Pressed(_player, action, axis);
                    Script_Event("Input_Pressed", new object[] { _player, action, axis });
                    break;
                case EInputState.Released:
                    Input_Released(_player, action, axis);
                    Script_Event("Input_Released", new object[] { _player, action, axis });
                    break;
                case EInputState.Down:
                    Input_Update(_player, action, dt, axis);
                    Script_Event("Input_Update", new object[] { _player, action, dt, axis });
                    break;
            }
        }
        
        
    }
    
    public void Draw(double dt, EDrawFlags flags, int state)
    {
        if (is_visible)
        {
            if (state == 0)
            {
                // Diagnostic-only: attributes a comp's own draw cost (not its children's) to
                // its type, so an expensive OnDraw* can be spotted without a profiler attach.
                double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
                OnDraw3D(dt, flags);
                if (ImpProfiler.enabled) ImpProfiler.DrawType_Add(GetType(), ImpProfiler.Now_Ms - t0);
            }

            Imp2D self2d = this as Imp2D;
            // Each comp draws its own content unrotated inside its own turned matrix; children
            // do the same with their own composed rotation, so nothing gets turned twice.
            bool needs_dim = state == 1 && self2d != null
                && (self2d.clip_children || self2d.transform.rotation != 0 || self2d.Scene_IsContent());
            TDimensions2 self_dim = needs_dim ? self2d.Dimensions_Get() : default;

            if (state == 1)
            {
                bool turned = Imp2D.Rotate_Push(self_dim);
                double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
                OnDraw2D(dt, flags);
                if (ImpProfiler.enabled) ImpProfiler.DrawType_Add(GetType(), ImpProfiler.Now_Ms - t0);
                Imp2D.Rotate_Pop(turned);
            }

            bool clip = state == 1 && self2d != null && self2d.clip_children;
            if (clip) Imp2D.Clip_Push(self_dim);

            int n = children.Count;
            ImpComp[] snap = ChildSnap_Rent(n);
            for (int i = 0; i < n; i++)
            {
                ImpComp c = snap[i];
                if (c.parent != this) continue;
                c.Draw(dt, flags, state);
            }
            ChildSnap_Return(snap, n);

            if (clip) Imp2D.Clip_Pop();
            if (state == 1)
            {
                bool turned = Imp2D.Rotate_Push(self_dim);
                double t0 = ImpProfiler.enabled ? ImpProfiler.Now_Ms : 0;
                OnDraw2DForeground(dt, flags);
                if (ImpProfiler.enabled) ImpProfiler.DrawType_Add(GetType(), ImpProfiler.Now_Ms - t0);
                Imp2D.Rotate_Pop(turned);
            }
        }
    }
    
    // ----------------------------------------------------
    // virtuals
    // ----------------------------------------------------
    
    public virtual void OnDraw2D(double dt, EDrawFlags flags) { }
    public virtual void OnDraw2DForeground(double dt, EDrawFlags flags) { }
    public virtual void OnDraw3D(double dt, EDrawFlags flags) { }
    
    [ScriptOverride] public virtual void OnBegin()
    {
        Owned_Bind();
        int n = children.Count;
        ImpComp[] snap = ChildSnap_Rent(n);
        for (int i = 0; i < n; i++)
        {
            ImpComp c = snap[i];
            if (c.parent != this)
            {
                continue;
            }
            c.OnBegin();
        }
        ChildSnap_Return(snap, n);
    }

    [ScriptOverride] public virtual void OnEnd()
    {
        int n = children.Count;
        ImpComp[] snap = ChildSnap_Rent(n);
        for (int i = 0; i < n; i++)
        {
            ImpComp c = snap[i];
            if (c.parent != this)
            {
                continue;
            }
            c.OnEnd();
        }
        ChildSnap_Return(snap, n);
    }

    protected virtual void OnDestroy() { }

    [ScriptOverride] public virtual void OnUpdate(double dt) { }

    [ScriptCall] public void Print(string text)
    {
        if (text == null)
        {
            text = "";
        }
        Console.WriteLine(text);
    }
    
    public virtual void OnVisibilityChange(bool _is_visible) { }
    
    // ----------------------------------------------------
    // Cursor
    // ----------------------------------------------------
    public virtual void Cursor_OnEvent(ImpPlayer player, ECursorEvent evnt) {}

    [ScriptOverride] public virtual void OnPopupSelect(TPopupMenuOption option) { }
    
    // ----------------------------------------------------
    // Cursor Grab (Drag & Drop)
    // ----------------------------------------------------
    public virtual bool CursorGrab_IsEnabled(ImpPlayer player) { return false;}
    //What this comp hands over when dropped somewhere. Null means it carries nothing.
    public virtual object CursorGrab_Payload() { return null; }

    // ----------------------------------------------------
    // Input
    // ----------------------------------------------------
    ImpComp[] ChildSnap_Rent(int n)
    {
        if (n == 0) return null;
        ImpComp[] snap = System.Buffers.ArrayPool<ImpComp>.Shared.Rent(n);
        children.CopyTo(snap);
        return snap;
    }

    static void ChildSnap_Return(ImpComp[] snap, int n)
    {
        if (snap == null) return;
        Array.Clear(snap, 0, n);
        System.Buffers.ArrayPool<ImpComp>.Shared.Return(snap);
    }

    [ScriptCall]
    public virtual void Input_SetOwnerActive(int player_id, bool is_active)
    {
        if (is_active)
        {
            input_owner=ImpPlayer.players[player_id];
            if (input_owner != null)
            {
                Console.WriteLine("Assigned input to : "+input_owner);
            }
        }
        else
        {
            input_owner = null;
        }
    }
    
    [ScriptOverride][Title(" Input - On Pressed")]
    public virtual void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis) { }
    [ScriptOverride][Title(" Input - On Released")]
    public virtual void Input_Released(ImpPlayer player, TLabel iaction, Vector3 axis) { }
    [ScriptOverride][Title(" Input - On Down")]
    public virtual void Input_Update(ImpPlayer player, TLabel iaction, double dt, Vector3 axis) { }
    
    
    //----------------------------------------------------------------
    
    public virtual void _Notify_AsCursorTarget(ImpPlayer player, ENotifyGeneric notify, double dt) { }
    public virtual void _Notify_AsGrabbedTarget(ImpPlayer player, ENotifyGeneric notify, double dt) { }
    public virtual void _Notify_OnGrabDrop(ImpPlayer player, ENotifyGrabTarget notify, ImpComp other, double dt) { }
    public virtual void _Notify_AsFocusTarget(ImpPlayer player, ENotifyGeneric notify, double dt) { }
}




