using ImperiumEngine.Assets;
using ImperiumEngine.Main;

namespace ImperiumEngine.Comps._3D;

public class C3_Image : ImpComp3D
{
    [ImpVar][Export] A_Texture image;
    [ImpVar][Export] bool is_billboard;
}