using Engine.Assets;

namespace Engine.Comps._1D.Abilities;

/*
 * base class for abilities that trigger animations. common type for thigns like MMO or Real-Time-With-Pause RPG abilities.
 */
public class Ab_AnimAction : C1_Ability
{
    public override void OnAbility_Activate(object Context)
    {
        base.OnAbility_Activate(Context);
        if (owning_creature.skeleton != null)
        {
            A_SkeletonAnim anim = null; //get from context somehow
            owning_creature.skeleton.PlayAnimation(anim, (n) =>
            {
                if (n == "impact")
                {
                    
                }
            }, () =>
            {
                Ability_Stop(false);
            });
        }
        
    }
}