using Engine.Assets;
using R3D_cs;
using Raylib_cs;
using Material = R3D_cs.Material;

namespace Engine.Assets.Materials;

public class A_M3_Object : A_Material
{
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // CLASS
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [ImpVar] public TMaterialCommons config;

    protected override Material Material_Build()
    {
        Material mat = R3D.GetDefaultMaterial();
        config.Apply(ref mat);
        return mat;
    }

    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // STATICS
    // +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    [Builtin] public static A_M3_Object PROTO_WHITE = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.White}
    };

    [Builtin] public static A_M3_Object PROTO_BLACK = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Black}
    };

    [Builtin] public static A_M3_Object PROTO_GRAY = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Gray}
    };
    [Builtin] public static A_M3_Object PROTO_RED = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Red}
    };
    [Builtin] public static A_M3_Object PROTO_GREEN = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Green}
    };
    [Builtin] public static A_M3_Object PROTO_BLUE = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Blue}
    };
    [Builtin] public static A_M3_Object PROTO_YELLOW = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Yellow}
    };
    [Builtin] public static A_M3_Object PROTO_PURPLE = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Purple}
    };
    [Builtin] public static A_M3_Object PROTO_ORANGE = new()
    {
        config = new() {color_map = A_Texture.S_PROTO_FLOOR, color_tint = Color.Orange}
    };
}