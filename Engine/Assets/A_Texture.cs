using System.Numerics;
using ImperiumEngine.Enums;
using ImperiumEngine.Structs;
using R3D_cs;
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
    [ImpVar] public float saturation=1.0f;
    [ImpVar] public float brightness=1.0f;
    [ImpVar] public EImageLayout ui_layout=EImageLayout.Stretch;

    bool _gpu_3d;

    public override void Source_OnReload(ImpFile file)
    {
        base.Source_OnReload(file);
        int i = source_index;
        if (i < 0 || i >= file.src_textures.Count) return;
        texture = file.src_textures[i];
        if (pixel_format != 0) texture.Format = pixel_format;
        _gpu_3d = false;
    }

    public Texture2D Gpu_Bind3D()
    {
        if (texture.Id == 0)
        {
            return texture;
        }
        if (!_gpu_3d)
        {
            Raylib.SetTextureWrap(texture, TextureWrap.Repeat);
            Raylib.GenTextureMipmaps(ref texture);
            Raylib.SetTextureFilter(texture, TextureFilter.Trilinear);
            _gpu_3d = true;
        }
        return texture;
    }
    
    // #################################################################################
    // Static
    // #################################################################################

    public static void Draw(A_Texture texture, TLayout2 config)
    {
        
    }
    
    // -------------------------------------
    // Built-ins
    // -------------------------------------
    public static A_Texture? PANEL_A = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Panel_A.png");
    public static A_Texture? PANEL_B = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Panel_B.png");
    
    public static A_Texture? BTN_A = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Button_A.png");
    
    public static A_Texture? TAB_A = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_Tab_A.png");
    
    public static A_Texture? ICO_SAVE = ImpAsset.Import<A_Texture>("{engine}/Icons/ico_editor_save.png");
    public static A_Texture? ICO_PLAY = ImpAsset.Import<A_Texture>("{engine}/Icons/ico_editor_play.png");
    public static A_Texture? ICO_STOP = ImpAsset.Import<A_Texture>("{engine}/Icons/ico_editor_stop.png");
    public static A_Texture? ICO_SCENE = ImpAsset.Import<A_Texture>("{engine}/Icons/ico_editor_scene.png");
    public static A_Texture? ICO_ARROW_R = Import<A_Texture>("{engine}/Icons/ico_editor_arrowR.png");
    public static A_Texture? ICO_ARROW_D = Import<A_Texture>("{engine}/Icons/ico_editor_arrowD.png");
    
    public static A_Texture? CHECKBOX_T = Import<A_Texture>("{engine}/Textures/UI/UI_Editor_CheckBox_T.png");
    public static A_Texture? CHECKBOX_F = Import<A_Texture>("{engine}/Textures/UI/UI_Editor_CheckBox_F.png");
    
    public static A_Texture? SEPERATOR_V = ImpAsset.Import<A_Texture>("{engine}/Textures/UI/UI_Editor_SeperatorV.png");

    public static A_Texture? GRAPH_NODE_BODY = Import<A_Texture>("{engine}/Textures/Graph/RegularNode_body.png");
    public static A_Texture? GRAPH_NODE_SPILL = Import<A_Texture>("{engine}/Textures/Graph/RegularNode_color_spill.png");
    public static A_Texture? GRAPH_NODE_GLOSS = Import<A_Texture>("{engine}/Textures/Graph/RegularNode_title_gloss.png");
    public static A_Texture? GRAPH_NODE_HIGHLIGHT = Import<A_Texture>("{engine}/Textures/Graph/RegularNode_title_highlight.png");
    public static A_Texture? GRAPH_NODE_SHADOW = Import<A_Texture>("{engine}/Textures/Graph/RegularNode_shadow.png");
    public static A_Texture? GRAPH_NODE_SHADOW_SEL = Import<A_Texture>("{engine}/Textures/Graph/RegularNode_shadow_selected.png");
    public static A_Texture? GRAPH_VAR_BODY = Import<A_Texture>("{engine}/Textures/Graph/VarNode_body.png");
    public static A_Texture? GRAPH_VAR_SPILL = Import<A_Texture>("{engine}/Textures/Graph/VarNode_color_spill.png");
    public static A_Texture? GRAPH_VAR_GLOSS = Import<A_Texture>("{engine}/Textures/Graph/VarNode_gloss.png");
    public static A_Texture? GRAPH_VAR_SHADOW = Import<A_Texture>("{engine}/Textures/Graph/VarNode_shadow.png");
    public static A_Texture? GRAPH_VAR_SHADOW_SEL = Import<A_Texture>("{engine}/Textures/Graph/VarNode_shadow_selected.png");
    
    public static A_Texture? ICO_COMP = Import<A_Texture>("{engine}/Thumbnails/ImpComp.png");
    public static A_Texture? ICO_COMP2D = Import<A_Texture>("{engine}/Thumbnails/Imp2D.png");
    public static A_Texture? ICO_COMP3D = Import<A_Texture>("{engine}/Thumbnails/Imp3D.png");
    public static A_Texture? THUMB_FILE = ImpAsset.Import<A_Texture>("{engine}/Thumbnails/_file.png");
    public static A_Texture? THUMB_FOLDER = ImpAsset.Import<A_Texture>("{engine}/Thumbnails/_folder.png");
    public static A_Texture? THUMB_FOLDER_OPEN = ImpAsset.Import<A_Texture>("{engine}/Thumbnails/_folder_open.png");

    public static A_Texture? S_PROTO_FLOOR=Import<A_Texture>("{engine}/Textures/Surface/Prototype/T_editor_S_proto_floor.png");
    public static A_Texture? S_PROTO_DOOR=Import<A_Texture>("{engine}/Textures/Surface/Prototype/T_editor_S_proto_door.png");
    public static A_Texture? S_PROTO_STAIR=Import<A_Texture>("{engine}/Textures/Surface/Prototype/T_editor_S_proto_stair.png");
    public static A_Texture? S_PROTO_WINDOW=Import<A_Texture>("{engine}/Textures/Surface/Prototype/T_editor_S_proto_window.png");

    public override Texture2D? Editor_GetThumbnail_Texture()
    {
        if (texture.Id != 0) return texture;
        return base.Editor_GetThumbnail_Texture();
    }
}

public class A_TextureHDR : A_Texture
{
    public Cubemap cubemap;
    public AmbientMap ambient;

    public override void Source_OnReload(ImpFile file)
    {
        if (cubemap.Size > 0)
        {
            R3D.UnloadCubemap(cubemap);
        }
        if (ambient.Irradiance != 0)
        {
            R3D.UnloadAmbientMap(ambient);
        }
        cubemap = default;
        ambient = default;
        base.Source_OnReload(file);
    }

    public void Cubemap_Ensure()
    {
        if (cubemap.Size > 0)
        {
            return;
        }
        string path = "";
        if (source_file != null && !string.IsNullOrEmpty(source_file.filepath))
        {
            path = ImpFile.Path_Resolve(source_file.filepath);
        }
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }
        cubemap = R3D.LoadCubemap(path, R3D_cs.CubemapLayout.Panorama);
        if (cubemap.Size > 0)
        {
            ambient = R3D.GenAmbientMap(cubemap, AmbientFlags.Illumination | AmbientFlags.Reflection);
        }
    }

    public static A_TextureHDR SKY_DAY_1=Import<A_TextureHDR>("{engine}/Textures/HDRI/sky_1.hdr");
    public static A_TextureHDR SKY_DAY_2=Import<A_TextureHDR>("{engine}/Textures/HDRI/sky_2.hdr");
}
