using Raylib_cs;

namespace ImperiumEngine.Assets;

[AssetColor(220, 140, 50)]
public class A_Texture : ImpAsset
{
    // #################################################################################
    // Class
    // #################################################################################
    
    public Texture2D texture;
    [ImpVar] public PixelFormat pixel_format;
    [ImpVar] public float hue;
    [ImpVar] public float saturation=0.1f;
    [ImpVar] public float brightness=1.0f;
    
    public override void Source_OnReload(ImpFile file)
    {
        base.Source_OnReload(file);
        int i = source_index;
        if (i < 0 || i >= file.src_textures.Count) return;
        texture = file.src_textures[i];
        if (pixel_format != 0) texture.Format = pixel_format;
    }
    
    // #################################################################################
    // Static
    // #################################################################################
    
    public static A_Texture? PANEL_A = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Panel_A.png");
    public static A_Texture? PANEL_B = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Panel_B.png");
    
    public static A_Texture? BTN_A = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Button_A.png");
    
    public static A_Texture? TAB_A = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Tab_A.png");
    
    public static A_Texture? ICO_SAVE = ImpAsset.Import<A_Texture>("{engine}/Icons/ico_editor_save.png");
    public static A_Texture? ICO_PLAY = ImpAsset.Import<A_Texture>("{engine}/Icons/ico_editor_play.png");
    public static A_Texture? ICO_SCENE = ImpAsset.Import<A_Texture>("{engine}/Icons/ico_editor_scene.png");
    public static A_Texture? ICO_ARROW_R = Import<A_Texture>("{engine}/Icons/ico_editor_arrowR.png");
    public static A_Texture? ICO_ARROW_D = Import<A_Texture>("{engine}/Icons/ico_editor_arrowD.png");
    public static A_Texture? ICO_COMP = Import<A_Texture>("{engine}/Icons/Types/ImpComp.png");
    public static A_Texture? ICO_COMP2D = Import<A_Texture>("{engine}/Icons/Types/ImpComp2D.png");
    public static A_Texture? ICO_COMP3D = Import<A_Texture>("{engine}/Icons/Types/ImpComp3D.png");
    
    public static A_Texture? CHECKBOX_T = Import<A_Texture>("{engine}/Textures/UI/UI_Editor_CheckBox_T.png");
    public static A_Texture? CHECKBOX_F = Import<A_Texture>("{engine}/Textures/UI/UI_Editor_CheckBox_F.png");
    
    public static A_Texture? SEPERATOR_V = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_SeperatorV.png");

    public static A_Texture? THUMB_FILE = ImpAsset.Import<A_Texture>("{engine}/Thumbnails/_file.png");
    public static A_Texture? THUMB_FOLDER = ImpAsset.Import<A_Texture>("{engine}/Thumbnails/_folder.png");
    

    public override Texture2D? Editor_GetThumbnail_Texture()
    {
        if (texture.Id != 0) return texture;
        return base.Editor_GetThumbnail_Texture();
    }
}

public class A_TextureHDR : A_Texture
{

}
