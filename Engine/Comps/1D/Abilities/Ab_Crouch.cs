using Engine.Assets;

namespace Engine.Comps._1D.Abilities;

public class Ab_Crouch : C1_Ability
{
    public override void OnAbility_Activate(object Context)
    {
        base.OnAbility_Activate(Context);
        owning_creature.creature_root.move_mode=A_MoveMode.CROUCH;;
    }

    public override void OnAbility_Finished(bool _cancelled)
    {
        base.OnAbility_Finished(_cancelled);
        owning_creature.creature_root.move_mode=default_move_mode;;
    }
}