using System.Numerics;
using ImperiumCore.Structs;

namespace ImperiumCore.Classes;

public enum ELevelRunState
{
    Inactive, //mainly used in editor mode. Changes to active when Play-In-Editor is pressed.
    Active,
}


public class ImpLevel : ImpAsset
{
    //public ImpEntity _entity;
    public List<ImpComponent> Objects = new(); // root components of this level's tree
    public ELevelRunState state;
    public ImpApp App;

    public ImpLevel()
    {

    }

    public void Object_Add(ImpComponent obj)
    {
        if (obj != null && !Objects.Contains(obj)) Objects.Add(obj);
    }

    public void OnBegin(ImpApp app)
    {
        App = app;
        foreach (var o in Objects)
        {
            o.Component_Begin();
        }
    }

    public void OnEnd()
    {
        foreach (var o in Objects)
        {
            o.Component_End();
        }
    }

    public void OnUpdate(double dt)
    {
        // tree always ticks; state gates gameplay-only systems (later)
        foreach (var o in Objects)
        {
            o.Component_Update(dt);
        }

        switch (state)
        {
            case ELevelRunState.Active:
                break;
            case ELevelRunState.Inactive:
                break;
        }
    }

    public void OnDraw(ImpRender rhi, double dt)
    {
        var viewport = new TRect2(Vector2.Zero, new Vector2(rhi.ViewportSize.x, rhi.ViewportSize.y));
        foreach (var o in Objects)
        {
            o.Component_Layout(viewport);
        }
        foreach (var o in Objects)
        {
            o.Component_Draw(rhi, dt);
        }
    }
    

    public void Active_Start()
    {

    }

    public void Active_End()
    {

    }


}
