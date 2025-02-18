using ImperiumEngine.Source.Libs;

namespace ImperiumEngine.Source;
using static ImpLib_GUID;


public class ImpApp
{
    // =======================================================================
    // Objects
    // =======================================================================
    private List<ImpObject> objects_active = new();
    public bool Object_Register(ImpObject obj)
    {
        if(Object_IsActive(obj)) {return false;}
        obj.GUID = GUID_New();
        return true;
    }
    
    public bool Object_IsActive(ImpObject obj)
    {
        return objects_active.Contains(obj);
    }
    
    // =======================================================================
    // Components
    // =======================================================================
    
    public void Component_Register(ImpObject obj, ImpObject parent)
    {
        if(!Object_Register(obj)) {return;}
        //obj._parent = parent;
        obj.lifetime_state = 0;
        objects_active.Add(obj);
    }
    
    // =======================================================================
    // Process
    // =======================================================================
    
    public void App_Begin()
    {
        
    }
    
    public void App_Update()
    {
        foreach (var o in objects_active.ToList())
        {
            if (o!=null)
            {
                //On_Begin
                if (o.lifetime_state == 0)
                {
                    o.On_Begin();
                    o.lifetime_state = 1;
                }
                
                //Is being destroyed
                if (o.lifetime_state == 2)
                {
                    o.On_End();
                    objects_active.Remove(o);
                }
                else
                {
                    //On_Update
                    o.On_Update(0.01f);
                    
                    //On_Draw
                    o.On_Draw(0.01f);
                }
            }
        }
    }
    
    public void App_End()
    {
        
    }
}