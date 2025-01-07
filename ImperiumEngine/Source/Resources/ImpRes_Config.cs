using System.Numerics;
using ImperiumEngine.Source.Cores;

namespace ImperiumEngine.Source.Resources;

public class ImpRes_Config : ImpResource
{
    public Vector4 UiColor_Default = new Vector4(0, 0, 0, 0);
    public Vector4 UiColor_Selected = new Vector4(0.5f, 0.7f, 0.5f, 0.1f);
}