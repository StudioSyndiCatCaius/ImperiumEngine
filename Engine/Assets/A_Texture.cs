using ImperiumEngine.Main;
using ImperiumEngine.Resources;
using Raylib_cs;

namespace ImperiumEngine.Assets;

public class A_Texture : ImpAsset
{
    protected override ImpResource? Resource_Create(string full_path)
    {
        if (Path.GetExtension(full_path).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            return new RES_SVG();

        return new ImpResource_Texture();
    }
}

public class ImpResource_Texture : ImpResource
{
    public override void OnImport(string filepath)
    {
        if (!File.Exists(filepath)) return;

        var tex = Raylib.LoadTexture(filepath);
        if (tex.Id != 0) r_texture.Add(tex);
    }
}
