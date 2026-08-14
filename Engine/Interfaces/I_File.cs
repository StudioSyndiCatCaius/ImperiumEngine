using ImperiumEngine.Assets;
using ImperiumEngine.Comps._1D;
using Raylib_cs;

namespace ImperiumEngine.Interfaces;

public class I_File
{
    //Tirggered when you double click a file in the file browser
    public virtual void Editor_File_Open()
    {
        
    }
    
    public virtual List<TPopupMenuOption> Editor_File_GetOptions()
    {
        return null;
    }

    public virtual Texture2D? Editor_GetThumbnail_Texture()
    {
        // needs support for things like previewing a png
        return null;
    }
    
    public virtual Color Editor_GetThumbnail_Color()
    {
        // needs support for things like previewing a png
        return Color.White;
    }
}