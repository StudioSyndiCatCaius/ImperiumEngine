using ImperiumCore.Structs;

namespace ImperiumCore.Classes;

// #####################################################################################################################
// OBJECT
// #####################################################################################################################

public class ImpComponent : ImpObject
{
    public ImpComponent _parent;
    
    //whether on not this should recieve updates
    [ImpVar][Exposed] public bool updating=true;
    //whether on not this should recieve draw calls
    [ImpVar][Exposed] public bool visible=true;
    
    // -------------------------------------------------------
    // VIRTUALS
    // -------------------------------------------------------
    
    //when the owning level playime begins
    public virtual void OnBegin()
    {
        
    }
    
    //when the owning level playime stops
    public virtual void OnEnd()
    {
        
    }
    
    public virtual void OnUpdate(double dt)
    {
        
    }
    
    public virtual void OnDraw(ImpRender rhi, double dt)
    {
        
    }
    
    // -------------------------------------------------------
    // Parent
    // -------------------------------------------------------

    [Exposed]
    public bool Parent_Is(ImpComponent parent, bool recursive = false)
    {
        return false;
    }
    
    // -------------------------------------------------------
    // Child
    // -------------------------------------------------------
    
    [Exposed]
    public  void Child_Add(ImpComponent child)
    {
        child._parent = this;
    }
    
    [Exposed]
    public void Child_Remove(ImpComponent child)
    {
        
    }
    
    [Exposed]
    public void Child_Index(Int32 index)
    {
        
    }
    
        
    // -------------------------------------------------------
    // Children
    // -------------------------------------------------------
    public List<ImpComponent> Children_GetAll(bool recursive = false)
    {
        return new List<ImpComponent>();
    }
    
    public void Children_RemoveAll()
    {
        
    }
}

// #####################################################################################################################
// 2D
// #####################################################################################################################

public class ImpComponent2D : ImpComponent
{
    [ImpVar][Exposed] public TTransform2D transform;
    [ImpVar][Exposed] public ImpAsset_2DTheme override_theme;
    
}

// #####################################################################################################################
// 3D
// #####################################################################################################################

public class ImpComponent3D : ImpComponent
{
    [ImpVar][Exposed] public TTransform3D transform;
}

