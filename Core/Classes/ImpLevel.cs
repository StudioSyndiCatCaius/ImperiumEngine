namespace ImperiumCore.Classes;

public enum ELevelRunState
{
    Inactive, //mainly used in editor mode. Changes to active when Play-In-Editor is pressed.
    Active,
}


public class ImpLevel : ImpAsset
{
    //public ImpEntity _entity;
    public List<ImpComponent> Objects;
    public ELevelRunState state;
    public ImpApp App;
    
    public ImpLevel()
    {
        
    }

    public void OnBegin(ImpApp app)
    {
        
    }
    
    public void OnEnd()
    {
        
    }
    
    public void OnUpdate(double dt)
    {
        //common
        
        
        switch (state)
        {
            case ELevelRunState.Active:
                
                foreach (var o in Objects)
                {
                    o.OnUpdate(dt);
                }
                
                break;
            case ELevelRunState.Inactive:
                break;
        }
    }
    
    public void OnDraw(ImpRender rhi, double dt)
    {
        foreach (var o in Objects)
        {
            o.OnDraw(rhi, dt);
        }
        
        switch (state)
        {
            case ELevelRunState.Active:
                break;
            case ELevelRunState.Inactive:
                break;
        }
        
    }
    
    public void Active_Start()
    {
        
    }
    
    public void Active_End()
    {
        
    }
    
    
}