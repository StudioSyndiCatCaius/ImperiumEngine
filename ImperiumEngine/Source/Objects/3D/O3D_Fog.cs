using System.Drawing;

namespace ImperiumEngine.Source.Objects._3D;

public class O3D_Fog : ImpComponent3D
{
    public float FogDensity=100f;
    public float FogHeightOffset;
    public float FogHeightSharpness;
    public float FogDistanceOffset;
    public float FogDistanceCutoff;
    public Color FogColor;
}