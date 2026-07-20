using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects.Assets;
using ImperiumEngine.Structs;
using Raylib_cs;

namespace ImperiumEngine.Classes;


// the main class for composing scene/entities. A fusion of Nodes (Godot) & Actors/Components (in uE)
public class ImpComponent
{
    [ImpVar] public Guid guid = Guid.NewGuid();

    //allows drawing
    [ImpVar] public bool is_visible = true;

    //allows update ticking
    [ImpVar] public bool is_active = true;

    // --------------------------------------
    // Hierarchy
    // --------------------------------------
    public ImpComponent? parent;
    readonly List<ImpComponent> children = new();

    public IReadOnlyList<ImpComponent> Children => children;

    public int Child_IndexOf(ImpComponent child) => children.IndexOf(child);

    //true if `other` sits anywhere below this component
    public bool IsAncestorOf(ImpComponent other)
    {
        for (var p = other.parent; p != null; p = p.parent)
            if (p == this) return true;
        return false;
    }

    //detaches from the current parent and inserts under new_parent at index (-1 = append).
    //returns false (no-op) if the move would create a cycle
    public bool Parent_Set(ImpComponent? new_parent, int index = -1)
    {
        if (new_parent == this) return false;
        if (new_parent != null && IsAncestorOf(new_parent)) return false;

        var old_parent = parent;
        if (old_parent != null)
        {
            int old_index = old_parent.children.IndexOf(this);
            old_parent.children.RemoveAt(old_index);
            //moving within the same parent: removal shifted everything after us left
            if (old_parent == new_parent && index > old_index) index--;
        }

        parent = new_parent;
        if (new_parent != null)
        {
            if (index < 0 || index > new_parent.children.Count) index = new_parent.children.Count;
            new_parent.children.Insert(index, this);
        }
        return true;
    }

    // --------------------------------------
    // Life
    // --------------------------------------

    //Runtime START
    public virtual void OnBegin() { }
    //Runtime END on object destroyed
    public virtual void OnEnd() { }
    //Runtime UPDATE
    public virtual void OnUpdate(double delta) { }

    //on init. plays before OnBegin, and plays in editor (equivalent of OnConstruction in Unreal Engine)
    public virtual void OnInit() { }

    //On drawn in runtime or editor. Called once per render pass each frame:
    //  1. inside R3D.Begin/End — submit R3D meshes/models here (DEBUG_PASS not set)
    //  2. inside raylib's BeginMode3D — raylib shapes/billboards/gizmos here (DEBUG_PASS set)
    public virtual void OnDraw(double delta, Camera3D cam, EDrawFlags flags) { }

    // --------------------------------------
    // Recursive dispatch — call these on roots; they walk the hierarchy,
    // gating whole subtrees on the parent's is_visible/is_active
    // --------------------------------------

    public void Init() { OnInit(); foreach (var c in children) c.Init(); }
    public void Begin() { OnBegin(); foreach (var c in children) c.Begin(); }
    public void End() { foreach (var c in children) c.End(); OnEnd(); }

    public void Update(double delta)
    {
        if (!is_active) return;
        OnUpdate(delta);
        foreach (var c in children) c.Update(delta);
    }

    public void Draw(double delta, Camera3D cam, EDrawFlags flags)
    {
        if (!is_visible) return;
        OnDraw(delta, cam, flags);
        foreach (var c in children) c.Draw(delta, cam, flags);
    }

    // --------------------------------------
    // Config
    // --------------------------------------

    protected virtual bool IsSingleton() => false;
    protected virtual bool Editor_AllowAdd() => true;
}

// ============================================================================================================
// 2D
// ============================================================================================================

public class ImpComponent2D : ImpComponent
{
    [ImpVar] public TTransform2D transform;
}

// ============================================================================================================
// 3D
// ============================================================================================================

public class ImpComponent3D : ImpComponent
{
    //local — relative to the nearest ImpComponent3D ancestor (world-space when unparented)
    [ImpVar] public TTransform3D transform;

    //local-space bounds (before scale/rotation/position) — used by the editor for
    //click-picking and selection outlines. Defaults to a unit cube.
    public virtual BoundingBox GetLocalBounds() =>
        new(new Vector3(-0.5f), new Vector3(0.5f));

    //nearest 3D ancestor; non-3D components in between are transform-transparent
    public ImpComponent3D? Parent3D
    {
        get
        {
            for (var p = parent; p != null; p = p.parent)
                if (p is ImpComponent3D p3) return p3;
            return null;
        }
    }

    //world-space TRS composed down the ancestor chain (scale composes componentwise; no shear)
    public void GetWorldTRS(out Vector3 pos, out Quaternion rot, out Vector3 scale)
    {
        pos = transform.Position;
        rot = ImpMath.EulerDegToQuat(transform.Rotation);
        scale = transform.Scale;

        var p3 = Parent3D;
        if (p3 == null) return;
        p3.GetWorldTRS(out var ppos, out var prot, out var pscale);
        scale *= pscale;
        rot = Quaternion.Concatenate(rot, prot);
        pos = ppos + Vector3.Transform(pos * pscale, prot);
    }

    //writes a world-space TRS back into the local transform
    public void SetWorldTRS(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var p3 = Parent3D;
        if (p3 != null)
        {
            p3.GetWorldTRS(out var ppos, out var prot, out var pscale);
            var inv = Quaternion.Inverse(prot);
            pos = SafeDiv(Vector3.Transform(pos - ppos, inv), pscale);
            rot = Quaternion.Concatenate(rot, inv);
            scale = SafeDiv(scale, pscale);
        }
        transform.Position = pos;
        transform.Rotation = ImpMath.QuatToEulerDeg(rot);
        transform.Scale = scale;
    }

    public Vector3 WorldPosition
    {
        get { GetWorldTRS(out var p, out _, out _); return p; }
    }

    public Matrix4x4 WorldMatrix
    {
        get
        {
            GetWorldTRS(out var p, out var r, out var s);
            return Matrix4x4.CreateScale(s)
                 * Matrix4x4.CreateFromQuaternion(r)
                 * Matrix4x4.CreateTranslation(p);
        }
    }

    //a zero parent scale collapses that axis — treat it as unrecoverable and return 0
    static Vector3 SafeDiv(Vector3 a, Vector3 b) => new(
        b.X != 0 ? a.X / b.X : 0,
        b.Y != 0 ? a.Y / b.Y : 0,
        b.Z != 0 ? a.Z / b.Z : 0);
}

// ============================================================================================================
// 3D Physics object
// ============================================================================================================

public class ImpPhysic3D : ImpComponent3D
{
    public Vector3 velocity;
}


// ============================================================================================================
// Level
// ============================================================================================================

//root class for a level entity. Not manually addable in editor.
public class ImpLevel : ImpComponent
{
    public A_Level level = new();

    protected override bool Editor_AllowAdd() => false;
}
