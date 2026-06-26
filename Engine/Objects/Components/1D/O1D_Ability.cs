using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumEngine.Structs;

namespace ImperiumEngine.Objects._1D;

public enum EAbilityEndReason : byte
{
    Completed,
    Cancelled,
}


//an ability that can be activated and used by a Creature
public class O1D_Ability : ImpComponent
{
    [ImpVar] public TTagSet AbilityTags;
    [ImpVar] public TTagSet CancelAbilities;
    [ImpVar] public TTagSet BlockAbilities;
    
    bool _isActive;
    O1D_Creature OwningCreature;
    
    public bool Ability_Activate()
    {
        if (_isActive) return false;
        _isActive = true;
        Ability_OnActivated();
        return true;
    }
    public void Ability_Deactivate(EAbilityEndReason reason)
    {
        if (!_isActive) return;
        _isActive = false;
        Ability_OnActivated(reason);
    }

    public override void OnUpdate(double dt)
    {
        base.OnUpdate(dt);
        if (_isActive)
        {
            Ability_OnActivateUpdate(dt);
        }
    }


    protected virtual void Ability_OnActivated() { }
    protected virtual void Ability_OnActivated(EAbilityEndReason reason) { }
    
    protected virtual void Ability_OnActivateUpdate(double dt) { }
    
    protected virtual double GetUtilityScore() => 0;
}