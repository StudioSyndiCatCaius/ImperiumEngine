using ImperiumEngine.Source.Assets;
using ImperiumEngine.Source.Objects.Assets;
using Silk.NET.OpenGL;

namespace ImperiumEngine.Source;
public class ImpObject : IDisposable
{
    public        FGuid GUID;
    public int   lifetime_state=0; //0= creating, 1=lifetime, 2=destroying

    public List<string> Tags = new();

    private List<ImpObject> _children = new List<ImpObject>();
    
    public void Native_Begin()
    {
        On_Begin();
    }

    public void Native_Update(float d)
    {
        foreach (var c in _children.ToList())
        {
            if (c.lifetime_state == 0)
            {
                c.lifetime_state = 1;
                c.Native_Begin();
            }
            else if(c.lifetime_state==2)
            {
                _children.Remove(c);
                c.Dispose();
            }
            else
            {
                c.Native_Update(d);
            }
        }
        On_Update(d);
    }

    public void Native_Draw(float d)
    {
        foreach (var c in _children)
        {
            if (c.lifetime_state == 1)
            {
                c.Native_Draw(d);
            }
        }
        On_Draw(d);
    }

    public void Native_End()
    {
        On_End();
    }

    public void DestroyObject()
    {
        if (!IsBeingDestroyed())
        {
            lifetime_state = 2;
        }
    }

    public bool IsBeingDestroyed()
    {
        return lifetime_state==2;
    }
    
    //Overidable
    public virtual void On_Begin() { }
    public virtual void On_End() { }
    public virtual void On_Update(float delta) { }
    public virtual void On_Draw(float delta) { }
    
    // ----------------------------------------------------------------------------------------------------------------
    // CHILDREN
    // ----------------------------------------------------------------------------------------------------------------
    public bool Add_Child(ImpObject child)
    {
        if (child != this && !_children.Contains(child))
        {
            _children.Add(child);
            return true;
        }
        return false;
    }
    
    public List<ImpObject> GetChildren(bool bAllDescendants=false)
    {
        if (bAllDescendants)
        {
            List<ImpObject>? output = null;
            foreach (var c in _children)
            {
                output.Concat(c.GetChildren(bAllDescendants));
            }
        }
        return _children;
    }

    public void Dispose()
    {
        Native_End();
    }
}

// ========================================================================================================================
// Components
// ========================================================================================================================
public class ImpComponent1D : ImpObject
{
    public bool bCanTick = true;
    public float TickFrequency;
    public IA_Script IaScript;
}

public class ImpComponent2D: ImpObject
{
    public FTransform2D Transform=new FTransform2D();
    public bool Visible=false;
}

public class ImpComponent3D: ImpObject
{
    public FTransform3D Transform =new FTransform3D();
    public bool         Visible=false;
    public GL           _gl;

    public ImpComponent3D()
    {
        _gl = Program.gl;
    }

    public override void On_Begin()
    {
        _gl = Program.gl;
        base.On_Begin();
    }

    public virtual void On_Draw3D(float delta, uint shader)
    {
        
    }
}

// ========================================================================================================================
// Assets
// ========================================================================================================================
public class ImpAsset: ImpObject
{
    private bool _isDirty = false;
    private string linked_file = "";
}