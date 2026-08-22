using Material = R3D_cs.Material;

namespace ImperiumEngine.Assets.Materials;

public class Mat_Object : A_Material
{
    [ImpVar] public TMaterialCommons commons = new();

    public override void Source_OnReload(ImpFile file)
    {
        base.Source_OnReload(file);
        if (file.src_models.Count == 0)
        {
            return;
        }
        R3D_cs.Model mdl = file.src_models[0];
        Span<Material> mats = mdl.Materials;
        int i = source_index;
        if (i < 0 || i >= mats.Length)
        {
            return;
        }
        commons = TMaterialCommons.FromMaterial(mats[i]);
    }

    protected override Material Material_Build()
    {
        return commons.ToMaterial();
    }
}
