// Imperium Engine Library. Contains all the core features for runing & managing game state & game data, agnostic to and 2D, 3D, or Audio.

namespace ImperiumEngine.Source.Cores;

public class ImpScene
{
    public List<ImpNode> nodes_active;

    public void Tick()
    {
        foreach (var node in nodes_active)
        {
            node.OnUpdate(0.0f);
            if (node.is_visible)
            {
                node.OnDraw3D();
                node.OnDraw2D();
            }
        }
    }
}

public class ImpNode
{
    public ImpNode parentNode;
    public string name;
    public List<string> tags;
    public bool is_visible;
    
    //Run when construct in the world
    public virtual void OnConstruct() {}
    //
    public virtual void OnBegin() {}
    //
    public virtual void OnEnd() {}
    //
    public virtual void OnUpdate(float deltaTime) {}
    //
    public virtual void OnDraw2D() {}
    //
    public virtual void OnDraw3D() {}
}


public class ImpResource
{
    
}