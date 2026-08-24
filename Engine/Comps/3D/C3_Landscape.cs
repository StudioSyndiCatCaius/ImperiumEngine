using System.Numerics;
using R3D_cs;

namespace ImperiumEngine.Comps._3D;


public struct TLandscapePoint
{
    public float height;
    public float[] paint_layer_weights;
}

public struct TLandscapeBrushState
{
    public Vector3 brush_position;
    public float brush_radius=1.0f;
    public float brush_strength=0.5f;
    public float brush_falloff=0.5f;

    public TLandscapeBrushState()
    {
        brush_position = default;
    }
}

public class C3_Landscape : Imp3D
{
    [ImpVar] public int tile_resolution = 16;
    [ImpVar] public int tile_size = 16;
    
    [ImpVar] private TLandscapePoint[] _points;

    private Mesh mesh;
    private Model model;
    
    public TLandscapePoint[] points;
}

// ########################################################################################################
// Landscape Post Process
// ########################################################################################################

public abstract class A_LandscapePostProcess : ImpAsset
{
    public abstract void Apply(C3_Landscape landscape);
}

// --------------------

public abstract class LandPP_AddNoise : A_LandscapePostProcess
{
    [ImpVar] public float noise_scale = 1.0f;
    [ImpVar] public float noise_strength = 1.0f;
    
}

