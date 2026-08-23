using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

//a component that affects a C1_Creature in some way
public class C1_Aura : ImpComp
{
    public C1_Creature target;
    public C1_Creature instigator;
    public object context;
    
    [ImpVar] public TTagSet tags;
    [ImpVar] public TTagSet effects_removed;
    
    public virtual bool CanApply(C1_Creature _target, C1_Creature _instigator, object _context) { return true;}
    
    public virtual void OnApply(C1_Creature _target, C1_Creature _instigator, object _context) {}
}