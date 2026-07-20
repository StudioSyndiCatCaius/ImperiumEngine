using ImperiumEngine.Interfaces;
using Raylib_cs;

namespace ImperiumEngine.Classes;

//An asset that can be save & loaded to and from disk. (.impasset). Similar to Resources in Godot (Or to a lesser extent, DataAssets in UE)
public class ImpAsset : I_EditorAsset
{
    public string file_link = "";   // the filepath to the asset on disk
    public string file_source = ""; // the filepath to the asset this will use if importing data (e.g texture, 3d model, sound, etc)

    // Set once at startup by the engine entry point
    public static string s_projectDir = "";
    public static string s_engineContentDir = "";

    
    
    /*
     * Resolve path keywords to absolute paths:
     *      {engine} / {Engine} : the Engine "Content" directory
     *      {editor} / {Editor} : the Engine "Content" directory
     *      {game}              : the Game/Project's "Content" directory.
     *          NOTE: if `CFG_Modding.modding_enabled` is true, will load from the game's Mod directory by matching file path
     *          Mod files are ONLY loaded at runtime, NOT editor. Editor will always use native game Content assets.
     */
    public static string ResolvePath(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        return source
            .Replace("{game}",   Path.Combine(s_projectDir, "Content"))
            .Replace("{engine}", s_engineContentDir)
            .Replace("{Engine}", s_engineContentDir)
            .Replace("{editor}", s_engineContentDir)
            .Replace("{Editor}", s_engineContentDir);
    }

    // I_EditorAsset — the editor's content browser reads thumbnails through these.
    // Subclasses customize by overriding the protected Editor_* virtuals.
    public Texture2D GetThumbnailTexture() => Editor_GetThumbnailTexture();
    public Color GetThumbnailColor() => Editor_GetThumbnailColor();
    public string GetExtension() => Editor_GetExtension();

    virtual protected Texture2D Editor_GetThumbnailTexture() => new Texture2D(); //Id 0 = use the editor's default doc thumbnail
    virtual protected Color Editor_GetThumbnailColor() => Color.White;
    virtual protected string Editor_GetExtension() => ".impasset";

    public virtual bool Load(string path)
    {
        file_link = path;
        return false;
    }
}
