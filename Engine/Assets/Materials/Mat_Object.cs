using Raylib_cs;

namespace ImperiumEngine.Assets.Materials;

public class Mat_Object : A_Material
{
    public TMaterialCommons commons;
    
    
    // --
    
    public static Mat_Object PROTO_STAIR=new()
    {
        commons = { color_map = Load<A_Texture>("{engine}/Textures/Surfaces/Prototype/T_Engine_S_proto_2.png")}
    };
    public static Mat_Object PROTO_DOOR=new()
    {
        commons = { color_map = Load<A_Texture>("{engine}/Textures/Surfaces/Prototype/T_Engine_S_proto_3.png")}
    };
    public static Mat_Object PROTO_WINDOW=new()
    {
        commons = { color_map = Load<A_Texture>("{engine}/Textures/Surfaces/Prototype/T_Engine_S_proto_4.png")}
    };
}