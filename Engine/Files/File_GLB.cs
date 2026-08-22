using ImperiumEngine.Assets;
using ImperiumEngine.Assets.Materials;
using R3D_cs;

namespace ImperiumEngine.Files;

public class File_GLB : ImpFile
{
    public File_GLB()
    {
        file_type = EFileType.Model;
        default_asset_type = typeof(A_Mesh);
    }

    public override List<TFileAssetOffer> Editor_ListCreateableAssets()
    {
        Reimport();
        List<TFileAssetOffer> list = new();
        if (src_models.Count == 0)
        {
            return base.Editor_ListCreateableAssets();
        }

        R3D_cs.Model mdl = src_models[0];
        if (src_textures.Count == 0)
        {
            Textures_CollectFromModel(mdl);
        }

        string stem = Path.GetFileNameWithoutExtension(filepath);
        if (string.IsNullOrEmpty(stem))
        {
            stem = "Asset";
        }

        for (int i = 0; i < src_textures.Count; i++)
        {
            list.Add(new TFileAssetOffer
            {
                asset_type = typeof(A_Texture),
                name = "texture_" + (i + 1),
                source_index = i,
            });
        }

        Span<R3D_cs.Material> mats = mdl.Materials;
        for (int i = 0; i < mats.Length; i++)
        {
            list.Add(new TFileAssetOffer
            {
                asset_type = typeof(Mat_Object),
                name = "material_" + (i + 1),
                source_index = i,
            });
        }

        if (mdl.Meshes.Length > 0)
        {
            string mesh_name = stem;
            try
            {
                Span<MeshName> names = mdl.MeshNames;
                if (names.Length == 1)
                {
                    string n = names[0].ToString();
                    if (!string.IsNullOrWhiteSpace(n))
                    {
                        mesh_name = n;
                    }
                }
            }
            catch
            {
            }
            list.Add(new TFileAssetOffer
            {
                asset_type = typeof(A_Mesh),
                name = mesh_name,
                source_index = 0,
            });
        }

        try
        {
            Skeleton skel = mdl.Skeleton;
            if (skel.Bones.Length > 0)
            {
                list.Add(new TFileAssetOffer
                {
                    asset_type = typeof(A_Skeleton),
                    name = stem + "_skel",
                    source_index = 0,
                });
            }
        }
        catch
        {
        }

        try
        {
            AnimationLib lib = R3D.LoadAnimationLib(filepath);
            Span<Animation> anims = lib.Animations;
            if (anims.Length > 0)
            {
                for (int i = 0; i < anims.Length; i++)
                {
                    string n = anims[i].Name;
                    if (string.IsNullOrWhiteSpace(n))
                    {
                        n = "anim_" + (i + 1);
                    }
                    list.Add(new TFileAssetOffer
                    {
                        asset_type = typeof(A_Animation),
                        name = n,
                        source_index = i,
                    });
                }
                R3D.UnloadAnimationLib(lib);
            }
        }
        catch
        {
        }

        if (list.Count == 0)
        {
            return base.Editor_ListCreateableAssets();
        }
        return list;
    }
}
