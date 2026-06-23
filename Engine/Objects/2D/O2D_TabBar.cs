using ImperiumCore.Classes;

namespace ImperiumEngine.Objects._2D;

public struct TTabBarOption
{
    public string text;
}

public class O2D_TabBar : ImpComponent2D
{
    public List<TTabBarOption> options;
    public Action<int> OnSelect;
}