using System.Numerics;
using Raylib_cs;

namespace ImperiumEngine.Assets.Materials;

public class Mat_Surface : A_Material
{

    [ImpVar][Category("Wall")] public TMaterialCommons wall;
    [ImpVar][Category("Wall")] public float wall_tiling=1.0f;
    
    [ImpVar][Category("Slope")] public Vector3 slope_normal=new(0,1,0);
    [ImpVar][Category("Slope")] public float slope_offset;
    [ImpVar][Category("Slope")] public float slope_sharpness;
    
    [ImpVar][Category("Floor")] public bool use_floor;
    [ImpVar][Category("Floor")] public TMaterialCommons floor;
    [ImpVar][Category("Floor")] public float floor_tiling=1.0f;
    
    // statics
    public static Mat_Surface PROTO_TILE=new()
    {
        wall = { color_map = Load<A_Texture>("{engine}/Textures/Surfaces/Prototype/T_Engine_S_proto_1.png")}
    };
}