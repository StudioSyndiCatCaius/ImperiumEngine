namespace ImperiumEngine.Source.Interfaces;

public interface IGameplayTags
{
    FGameplayTag GetGameplayCategory()
    {
        return new FGameplayTag();
    }
    FGameplayTagContainer GetGameplayTags()
    {
        return new FGameplayTagContainer();
    }
}