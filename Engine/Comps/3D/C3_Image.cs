using Engine.Assets;
using Engine.Core;

namespace Engine.Comps._3D;

public class C3_Image : Imp3D
{
    [ImpVar] public A_Texture texture;
    [ImpVar] public bool is_billboard;
}