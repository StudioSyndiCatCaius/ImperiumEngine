using System.Numerics;
using Engine.Core;

namespace Engine.Comps._1D.Abilities;

public class Ab_Jump : C1_Ability
{
    [ImpVar] public float jump_height=5;
    [ImpVar] public float coyote_time=0.0f; //time (in sec) after fall begins when you can still jump
    
    public override void OnAbility_Activate(object Context)
    {
        base.OnAbility_Activate(Context);
        if (owning_creature.creature_root != null)
        {
            Vector3 jumpv = Imp.WORLD_UP * jump_height;
            owning_creature.creature_root.Phys_Launch(jumpv,false,true);
            Ability_Stop(false);
        }
    }
}