using ImperiumEngine.Source.Interfaces;

namespace ImperiumEngine.Source.Assets;

public class IA_GameplayCommon : ImpAsset, ICommonAsset, IGameplayTags
{
    public FText DisplayName = new FText();
    public FText GetDisplayName()
    {
        return DisplayName;
    }
    
    public FText DisplayDescription = new FText();
    public FText GetDisplayDescription()
    {
        return DisplayDescription;
    }
    
    public FGameplayTag GameplayCategory=new FGameplayTag();
    public FGameplayTag GetGameplayCategory()
    {
        return GameplayCategory;
    }
    
    public FGameplayTagContainer GameplayTags = new FGameplayTagContainer();
    public FGameplayTagContainer GetGameplayTags()
    {
        return GameplayTags;
    }
}