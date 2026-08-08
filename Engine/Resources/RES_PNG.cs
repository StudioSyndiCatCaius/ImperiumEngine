using ImperiumEngine.Main;
using Raylib_cs;

namespace ImperiumEngine.Resources;

public class RES_PNG  : ImpResource
{
    public override void OnImport(string filepath)
    {
        base.OnImport(filepath);
        Texture2D _txtr=Raylib.LoadTexture(filepath);
        r_texture.Add(_txtr);
    }
}