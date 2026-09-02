using Engine.Assets;
using Engine.Assets.Materials;
using Engine.Enums;
using Engine.Globals;
using Engine.Structs;
using R3D_cs;
using Raylib_cs;
using Mesh = R3D_cs.Mesh;
using Model = R3D_cs.Model;

namespace Engine.Core;

public class ImpFile
{

    // ==============================================================================================================
    // CLASS
    // ==============================================================================================================
    
    /*
     * the class of an ImpFile is used to store data loaded from a file. (E.G. png, wav, glb, etc)
     * it is READ ONLY and should not be modified, as it is never written to disk
     */
    
    public string filepath="";
    public bool store_bytes=false;
    public TByteBlock data=new();
    public EFileType file_type=EFileType.DataText;
    
    public List<Texture2D> src_textures = new();
    public List<Sound> src_sounds = new();
    public List<Model> src_models = new();
    public List<Animation> src_anims = new();
    public List<Font> src_fonts = new();

    
    public void Reimport()
    {
        string _path = GFile.Make_Path_Absolute(filepath);
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
        Invalidate();
        if(store_bytes) data = GFile.LoadAs_Bytes(_path);
        switch (file_type)
        {
            // ---- MODEL
            case EFileType.Texture:
                Texture2D _txt=Raylib.LoadTexture(_path);
                src_textures.Add(_txt);
                break;
            // ---- MODEL
            case EFileType.Sound:
                Sound _snd=Raylib.LoadSound(_path);
                src_sounds.Add(_snd);
                break;
            // ---- MODEL
            case EFileType.Model:
                src_models.Add(R3D.LoadModelEx(_path, ImportFlags.RetainMeshNames | ImportFlags.RetainMeshData));
                break;
            case EFileType.Animation:
                // ??
                break;
            case EFileType.Font:
                Font _fnt=Raylib.LoadFont(_path);
                src_fonts.Add(_fnt);
                break;
            default:
                break;
        }
    }

    public void Invalidate()
    {
        data = new();
        src_textures.Clear(); src_sounds.Clear(); src_models.Clear(); src_fonts.Clear(); src_anims.Clear();
    }

    // ------------------------------------------------------------
    // Asset getters
    // ------------------------------------------------------------
    
    public Texture2D get_Texture(int id, Texture2D fallback = default) { if (src_textures.Count > id) return src_textures[id]; return fallback; }
    public Sound get_Sound(int id, Sound fallback = default) { if (src_sounds.Count > id) return src_sounds[id]; return fallback; }
    public Model get_Model(int id, Model fallback = default) { if (src_models.Count > id) return src_models[id]; return fallback; }
    public Animation get_Animation(int id, Animation fallback = default) { if (src_anims.Count > id) return src_anims[id]; return fallback; }
    public Font get_Font(int id, Font fallback = default) { if (src_fonts.Count > id) return src_fonts[id]; return fallback; }
    
    
    /*
     *  in editor when running the "Create Asset" Dialog on this file, this creates and gets a list of all possible assets that can be created.
     *  filepath on a returned asset is a suggested stem (not a destination path).
     */
    public virtual List<ImpAsset> GetCreatableAsset()
    {
        List<ImpAsset> list = new();
        if (string.IsNullOrEmpty(filepath)) return list;

        void AddAnims()
        {
            string abs = GFile.Make_Path_Absolute(filepath);
            if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) return;
            try
            {
                AnimationLib lib = R3D.LoadAnimationLib(abs);
                try
                {
                    Span<Animation> anims = lib.Animations;
                    if (anims.Length <= 0) return;
                    for (int i = 0; i < anims.Length; i++)
                    {
                        string clip = "";
                        try { clip = anims[i].Name.ToString() ?? ""; }
                        catch { }
                        list.Add(new A_Animation
                        {
                            sourcefile = filepath,
                            source_id = i,
                            filepath = clip
                        });
                    }
                }
                finally
                {
                    R3D.UnloadAnimationLib(lib);
                }
            }
            catch { }
        }

        switch (file_type)
        {
            case EFileType.Texture:
                list.Add(new A_Texture { sourcefile = filepath });
                break;
            case EFileType.Sound:
                list.Add(new A_Sound { sourcefile = filepath });
                break;
            case EFileType.Font:
                list.Add(new A_Font { sourcefile = filepath });
                break;
            case EFileType.Model:
                list.Add(new A_Mesh { sourcefile = filepath, model_index = 0 });
                if (src_models.Count > 0)
                {
                    Model model = src_models[0];
                    if (R3D.IsSkeletonValid(model.Skeleton))
                        list.Add(new A_Skeleton { sourcefile = filepath, filepath = "SKEL" });
                    int mats = model.Materials.Length;
                    for (int i = 0; i < mats; i++)
                        list.Add(new A_M_Object { sourcefile = filepath, filepath = mats > 1 ? "M_" + (i + 1) : "M" });
                }
                AddAnims();
                break;
            case EFileType.Animation:
                AddAnims();
                break;
        }

        return list;
    }
}