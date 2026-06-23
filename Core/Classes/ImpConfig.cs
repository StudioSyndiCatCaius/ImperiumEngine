using ImperiumEngine.Interfaces;

namespace ImperiumCore.Classes;

//baseclasse for a settings category file
public class ImpConfig : I_Saveable
{
    public static ImpConfig Get<T>()
    {
        return default(ImpConfig);
    }
}