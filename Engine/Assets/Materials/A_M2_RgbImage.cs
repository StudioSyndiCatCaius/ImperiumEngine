using Raylib_cs;

namespace Engine.Assets.Materials;

/*
 *  an image tinted based on its rgb values
 */
public class A_M2_RgbImage : A_Material
{
    [ImpVar] public A_Texture texture;
    [ImpVar] public A_Texture rgb_map;
    [ImpVar] public Color tint_r=Color.Red;
    [ImpVar] public Color tint_g=Color.Green;
    [ImpVar] public Color tint_b=Color.Blue;
}