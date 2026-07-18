using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Objects.Assets;
using ImperiumEngine.Structs;

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

    void Child_Add(ImpComponent child)
    {
        children.Add(child);
    }

    void Child_Remove(ImpComponent child)
    {
        children.Remove(child);
    }

    void Children_Clear()
    {
        foreach (var child in children)
        {
            Child_Remove(child);
        }
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

    //On drawn in runtime or editor
    public virtual void OnDraw(double delta, EDrawFlags flags) { }

    //Debug/gizmo pass — runs in raylib's BeginMode3D (after R3D.End), where raylib
    //shape drawing works. Used for light shapes, bounding boxes, etc.
    public virtual void OnDrawDebug(double delta) { }

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
    [ImpVar] public TTransform3D transform;
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
