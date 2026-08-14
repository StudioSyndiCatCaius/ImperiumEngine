using ImperiumEngine.Assets;

namespace ImperiumEngine.Comps._3D;

public class C3_Image : ImpComp3D
{
    [ImpVar] A_Texture image;
    [ImpVar] bool is_billboard;
}