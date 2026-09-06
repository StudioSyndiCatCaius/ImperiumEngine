using System.Numerics;

namespace Engine.Assets.Materials;

/*
 * 
 */
public class A_M2_Layered3 : A_Material
{
   [ImpVar] public A_Texture layer1_texture;
   [ImpVar] public A_Texture layer2_texture;
   [ImpVar] public A_Texture layer3_texture;
   [ImpVar] public Vector2 scale=Vector2.One;
   [ImpVar] public Vector2 offset=Vector2.Zero;
}