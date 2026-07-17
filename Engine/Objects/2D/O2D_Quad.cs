using ImperiumEngine.Classes;
using ImperiumEngine.Objects.Assets;
using Raylib_cs;

namespace ImperiumEngine.Objects._2D;

public class O2D_Quad : ImpComponent2D
{
    [Exposed] public A_Texture2D texture;
    [Exposed] public Color tint;
}