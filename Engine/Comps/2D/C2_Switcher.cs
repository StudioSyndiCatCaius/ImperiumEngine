using Engine.Core;

namespace Engine.Comps._2D;

public class C2_Switcher : Imp2D
{
    [ImpVar] public int current_index;
    
    private int _last_index;

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (current_index != _last_index)
        {
            _last_index = current_index;
            //change child visibility
        }
    }
    public override bool ChildLayout_IsFree() { return false; }
}