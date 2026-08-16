using System.Numerics;
using ImperiumEngine.Assets;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Comps._1D;

public abstract class C1_Ability : ImpComp
{
    [ImpVar][Category("Input")] public TLabel input_label;
    [ImpVar][Category("Input")] public bool input_release_stops_ability;

    private bool is_active = false;
    public C1_Creature owning_creature;
    
    protected A_MoveMode default_move_mode;

    public override void OnBegin()
    {
        base.OnBegin();
        //cache default move mode
        if (owning_creature != null && owning_creature.creature_root!=null)
        {
            default_move_mode = owning_creature.creature_root.move_mode;
        }
    }

    public bool Ability_Activate(object Context)
    {
        if(is_active) return false;
        if (!Ability_CanActivate(Context)) return false;
        is_active = true;
        return true;
    }

    public virtual bool Ability_CanActivate(object Context)
    {
        return true;
    }

    public void Ability_Stop(bool _cancelled)
    {
        if (!is_active) return;
        is_active = false;
        OnAbility_Finished(_cancelled);
    }
    

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (is_active) OnAbility_Update(dt);
    }

    // ---------------------------------------------------------------
    // Input
    // ---------------------------------------------------------------
    
    public override void Input_Pressed(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        base.Input_Pressed(player, iaction, axis);
        if (iaction == input_label)
        {
            Ability_Activate(null);
        }
    }

    public override void Input_Released(ImpPlayer player, TLabel iaction, Vector3 axis)
    {
        base.Input_Released(player, iaction, axis);
        if (iaction == input_label && input_release_stops_ability)
        {
            Ability_Stop(false);
        }
    }
    
    // ---------------------------------------------------------------
    // Virtuals
    // ---------------------------------------------------------------
    
    public virtual void OnAbility_Activate(object Context) { }
    public virtual void OnAbility_Finished(bool _cancelled) { }
    public virtual void OnAbility_Update(double dt) { }
}