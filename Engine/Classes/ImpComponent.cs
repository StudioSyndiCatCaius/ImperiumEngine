using System.Numerics;
using ImperiumCore.Assets;
using ImperiumCore.Enums;
using ImperiumCore.Structs;
using ImperiumEngine.Classes;
using ImperiumEngine.Objects._1D;
using Silk.NET.Input;

namespace ImperiumCore.Classes;

// #####################################################################################################################
// OBJECT
// #####################################################################################################################

// Base of the scene/UI tree. The Component_* drivers walk the subtree; subclasses
// only implement the per-node On* hooks.
public class ImpComponent : ImpObject
{
    public ImpComponent? _parent;
    protected readonly List<ImpComponent> _children = new();

    //whether on not this should recieve updates
    [ImpVar][Exposed] public bool updating=true;
    //whether on not this should recieve draw calls
    [ImpVar][Exposed] public bool visible=true;

    // -------------------------------------------------------
    // VIRTUALS  (per-node hooks)
    // -------------------------------------------------------

    protected virtual void OnBegin() { }
    protected virtual void OnEnd() { }
    protected virtual void OnUpdate(double dt) { }
    protected virtual void OnDraw(ImpRender rhi, double dt) { }
    protected internal virtual void OnDraw3D(ImpRender rhi, O1D_Viewport viewport, ImpCamera camera, double dt) { }

    // -------------------------------------------------------
    // DRIVERS  (walk this node's subtree)
    // -------------------------------------------------------

    public void Component_Begin()
    {
        OnBegin();
        foreach (var c in _children) c.Component_Begin();
    }

    public void Component_End()
    {
        for (int i = _children.Count - 1; i >= 0; i--) _children[i].Component_End();
        OnEnd();
    }

    // non-2D nodes have no rect; pass the parent area straight through
    public virtual void Component_Layout(TRect2 parentRect)
    {
        foreach (var c in _children) c.Component_Layout(parentRect);
    }

    // !updating freezes the whole subtree.
    public void Component_Update(double dt)
    {
        if (!updating) return;
        OnUpdate(dt);
        foreach (var c in _children) c.Component_Update(dt);
    }

    // Parent draws behind its children; !visible hides the subtree.
    public void Component_Draw(ImpRender rhi, double dt)
    {
        if (!visible) return;
        OnDraw(rhi, dt);
        foreach (var c in _children) c.Component_Draw(rhi, dt);
    }

    private bool _destroyed=false;
    public bool Component_Destroy()
    {
        if (_destroyed) return false;
        //removes this component from the tree and queus it for deletion from memory
        return true;
    }

    // -------------------------------------------------------
    // Parent
    // -------------------------------------------------------

    [Exposed]
    public bool Parent_Is(ImpComponent parent, bool recursive = false)
    {
        if (_parent == null) return false;
        if (_parent == parent) return true;
        return recursive && _parent.Parent_Is(parent, true);
    }

    // -------------------------------------------------------
    // Child
    // -------------------------------------------------------

    [Exposed]
    public void Child_Add(ImpComponent child)
    {
        if (child == null || child == this) return;
        child._parent?.Child_Remove(child); // re-parent cleanly
        child._parent = this;
        _children.Add(child);
    }

    [Exposed]
    public void Child_Remove(ImpComponent child)
    {
        if (child != null && _children.Remove(child))
            child._parent = null;
    }

    [Exposed]
    public ImpComponent? Child_Index(Int32 index)
    {
        return (index >= 0 && index < _children.Count) ? _children[index] : null;
    }


    // -------------------------------------------------------
    // Children
    // -------------------------------------------------------
    public List<ImpComponent> Children_GetAll(bool recursive = false)
    {
        if (!recursive) return new List<ImpComponent>(_children);

        var all = new List<ImpComponent>();
        foreach (var c in _children)
        {
            all.Add(c);
            all.AddRange(c.Children_GetAll(true));
        }
        return all;
    }

    public void Children_RemoveAll()
    {
        foreach (var c in _children) c._parent = null;
        _children.Clear();
    }
}

