using Engine.Core;

namespace Engine.Assets;

public abstract class A_GlobalScript
{
    public virtual void Run(ImpGame game) { }
    
    public static void Run(List<A_GlobalScript> scripts)
    {
        foreach (var s in scripts)
        {
            s.Run(ImpGame.current);
        }
    }
}

public abstract class A_GlobalCondtion
{
    public virtual bool Check(ImpGame game) { return true; }
    

    public static bool Check(List<A_GlobalCondtion> conds)
    {
        foreach (var c in conds)
        {
            if(!c.Check(ImpGame.current)) { return false;}
        }
        return true;
    }
}