using System.Drawing;
using ImperiumCore;
using ImperiumCore.Classes;
using ImperiumCore.Classes.Components;

namespace ImperiumEngine.Objects._3D;

public abstract class O3D_Light : ImpComponent3D
{
    [ImpVar][Exposed] public Color LightColor;
    [ImpVar][Exposed] public float LightIntensity;
    [ImpVar][Exposed] bool CastShadows=true;
}






