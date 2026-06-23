using System.Drawing;
using ImperiumCore;
using ImperiumCore.Classes;
using Silk.NET.OpenGL;

namespace ImperiumEngine.Assets;

public class Material_Shader_Prop : ImpAsset_Material
{
    [ImpVar] public A_Texture color_map;
    [ImpVar] public Color color_tint;
    
    [ImpVar] public A_Texture normal_map;
    [ImpVar] public float normal_intensity;
    
    [ImpVar] public A_Texture specular_map;
    [ImpVar] public float specular_intensity;
}