using System.Numerics;

namespace ImperiumEngine.Source;

public class ImpObject : IDisposable
{
    public static bool AddChildToObject(ImpObject parent, ImpObject child)
    {
        return false;
    }
    
    public int LifetimeState=0;
    public Guid GUID=Guid.NewGuid();
    
    public void Dispose()
    {
        On_End();
    }
    
    public virtual void On_Begin() { }
    public virtual void On_Update(double delta) { }
    public virtual void On_Draw(double delta) { }
    public virtual void On_End() { }

    // Native
    public virtual void Native_Begin()
    {
        On_Begin();
    }
    public virtual void Native_Update(double delta)
    {
        On_Update(delta);
    }
    public virtual void Native_Draw(double delta)
    {
        On_Draw(delta);
    }

}

// ==============================================================================================================================
// Imp Asset
// ==============================================================================================================================
public abstract class ImpAsset : ImpObject
{
    private string url = "";
    private bool   bIsDirty;
}

// ==============================================================================================================================
// Imp Component
// ==============================================================================================================================
public abstract class ImpComponent : ImpObject
{
    public ImpComponent       _parent   = null;
    public List<ImpComponent> _children =new List<ImpComponent>();
    
    public List<string>       Labels = new();

    public List<ImpComponent> GetChildren(bool bAllDescendents)
    {
        return _children;
    }

    public bool Child_Add(ImpComponent child)
    {
        if (!_children.Contains(child))
        {
            _children.Add(child);
            child._parent = this;
            if (child.LifetimeState== 0)
            {
                child.Native_Begin();
            }
            return true;
        }
        return false;
    }
    
    public override void Native_Begin()
    {
        LifetimeState = 1;
        base.Native_Begin();
    }

    public override void On_Update(double delta)
    {
        foreach (var c in _children)
        {
            c?.On_Update(delta);
        }
        base.On_Update(delta);
    }

    public override void On_Draw(double delta)
    {    foreach (var c in _children)
        {
            c?.On_Draw(delta);
        }
        base.On_Draw(delta);
    }
}

public class ImpComponent1D : ImpComponent { }

public class ImpComponent2D : ImpComponent
{
    
    public bool         bFocused = false;
    public FTransform2D LocalTransform;
}

public class ImpComponent3D : ImpComponent
{
    public FTransform3D LocalTransform;

    public Vector3 Location_Get()
    {
        Vector3 extra = Vector3.Zero;
        if (_parent is ImpComponent3D _parent3D)
        {
            extra = _parent3D.Location_Get();
        }
        return LocalTransform.Location+extra;
    }

    public void Location_Set(Vector3 location)
    {
        LocalTransform.Location = location;
    }
    
    public virtual void On_Draw3D(double delta)
    {
        
    }
}