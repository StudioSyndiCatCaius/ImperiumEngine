using System.Drawing;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;

namespace ImperiumEngine.Objects._2D;

public class O2D_ProgressBar : ImpComponent2D
{
    public A_UiStyle_ProgressBar style;
    
    public Color color;
    public float percent;
}

public class A_UiStyle_ProgressBar : ImpAsset
{
    
}