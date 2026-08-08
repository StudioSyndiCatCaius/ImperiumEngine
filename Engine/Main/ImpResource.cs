using R3D_cs;
using Raylib_cs;
using Model = R3D_cs.Model;

namespace ImperiumEngine.Main;


// an ImpResource is a file loaded into memory, such as a texture sound or model.
public class ImpResource
{
    // ====================================================================================
    // Statics
    // ====================================================================================
    public static Dictionary<uint, ImpResource> files=new Dictionary<uint, ImpResource>();

    // second index so the same source file imported twice reuses one resource
    static Dictionary<string, uint> by_path = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

    public static ImpResource? Find(string filepath)
    {
        if (string.IsNullOrEmpty(filepath)) return null;
        if (!by_path.TryGetValue(filepath, out uint id)) return null;

        files.TryGetValue(id, out var res);
        return res;
    }

    // ====================================================================================
    // Class
    // ====================================================================================
    public string _filepath="";
    public uint _id=0;

    public List<Image> r_image=[];
    public List<Texture2D> r_texture=[];
    public List<Wave> r_wave=[];
    public List<Sound> r_sound=[];
    public List<Model> r_model=[];
    public List<Skeleton> r_skeleton=[];
    public List<Font> r_font=[];
    public List<FontType> r_fonttype=[];

    public void Import()
    {
        //clear out old data
        r_image = [];
        r_texture = [];
        r_wave = [];
        r_sound = [];
        r_model = [];
        r_skeleton = [];
        r_font = [];
        r_fonttype = [];

        //register under a fresh id the first time through
        if (_id == 0)
        {
            do { _id = (uint)Random.Shared.NextInt64(1, (long)uint.MaxValue + 1); }
            while (files.ContainsKey(_id));
        }

        files[_id] = this;
        if (_filepath != "") by_path[_filepath] = _id;

        //load new data
        OnImport(_filepath);
    }

    public virtual void OnImport(string filepath)
    {

    }

}
